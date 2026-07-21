using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;

namespace ECommerceBackend.API.HostedServices
{
    // Periodically reconciles Redis stock counters with the SQL source of truth, correcting any
    // drift (Redis restart/eviction, dead-lettered settlements, manual edits). Also fails
    // expired Pending orders. Multi-instance safe via a distributed lock.
    //
    // Invariant enforced:  Redis stock:{id}  ==  SQL StockQuantity  -  SUM(open Pending reservations)
    public class StockReconciliationService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<StockReconciliationService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private const string LockKey = "lock:stock-reconciliation";

        public StockReconciliationService(IServiceProvider services, ILogger<StockReconciliationService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var stockReservation = scope.ServiceProvider.GetRequiredService<IStockReservationRepository>();
                    var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                    var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

                    var token = await stockReservation.AcquireLockAsync(LockKey, TimeSpan.FromMinutes(2));
                    if (token is not null)
                    {
                        try
                        {
                            await FailExpiredOrdersAsync(orderRepo, stockReservation);
                            await ReconcileStockAsync(productRepo, orderRepo, stockReservation);
                        }
                        finally
                        {
                            await stockReservation.ReleaseLockAsync(LockKey, token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stock reconciliation loop failed");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        // Recompute the expected Redis value for the HOT set only (keys present in Redis)
        // and correct any drift. Scales to large catalogs — cold items are left to lazy-load.
        private async Task ReconcileStockAsync(
            IProductRepository productRepo,
            IOrderRepository orderRepo,
            IStockReservationRepository stockReservation)
        {
            // Only items currently in Redis can drift — scan for the hot set.
            var hotIds = await stockReservation.GetTrackedProductIdsAsync();
            if (hotIds.Count == 0) return;

            // Fetch SQL truth + held reservations ONLY for those ids.
            var sqlStocks = (await productRepo.GetStockForManyAsync(hotIds))
                .ToDictionary(x => x.Id, x => x.Stock);
            var pendingByProduct = await orderRepo.GetPendingReservedQuantitiesForAsync(hotIds);

            foreach (var id in hotIds)
            {
                if (!sqlStocks.TryGetValue(id, out int sqlStock))
                    continue; // product no longer exists in SQL

                pendingByProduct.TryGetValue(id, out int reservedHeld);
                int expected = sqlStock - reservedHeld;
                if (expected < 0) expected = 0;

                var actual = await stockReservation.GetStockAsync(id);

                // Fix drift only when the key still exists (it may have expired since the scan).
                if (actual is not null && actual.Value != expected)
                {
                    _logger.LogWarning(
                        "Stock drift for product {ProductId}: Redis={Actual}, expected={Expected}. Correcting.",
                        id, actual.Value, expected);
                    await stockReservation.SetStockAsync(id, expected);
                }
            }
        }

        // Pending orders whose reservation expired but were never confirmed/released -> mark Failed.
        private async Task FailExpiredOrdersAsync(
            IOrderRepository orderRepo,
            IStockReservationRepository stockReservation)
        {
            var expired = await orderRepo.GetExpiredPendingOrdersAsync(DateTime.UtcNow);
            foreach (var order in expired)
            {
                // The sweeper already returns stock to Redis on TTL; here we just settle the
                // order record so it isn't counted as an open reservation anymore.
                await orderRepo.UpdateStatusAsync(order.Id, OrderStatus.Failed);
                _logger.LogInformation("Reconciliation marked expired Pending order {OrderId} as Failed", order.Id);
            }
            await orderRepo.SaveChangesAsync();
        }
    }
}
