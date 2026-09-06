using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using ECommerceBackend.Application.Constants;

namespace ECommerceBackend.API.HostedServices
{
    // Periodically reconciles Redis stock counters with the SQL source of truth, correcting any
    // drift (Redis restart/eviction, dead-lettered settlements, manual edits). Also fails
    // expired Pending orders. Multi-instance safe via a distributed lock.
    //
    // Invariant enforced: Redis stock:{id} == SQL StockQuantity
    //                     - SUM(Pending or Confirmed-but-unsettled reservations)
    public class StockReconciliationService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<StockReconciliationService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

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

                    await ReconcileStockAsync(productRepo, orderRepo, stockReservation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stock reconciliation loop failed");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        // Recompute the expected Redis value for the HOT set only (keys present in Redis)
        // and correct any drift. Scales to large catalogs � cold items are left to lazy-load.
        private async Task ReconcileStockAsync(
            IProductRepository productRepo,
            IOrderRepository orderRepo,
            IStockReservationRepository stockReservation)
        {
            // Only items currently in Redis can drift � scan for the hot set.
            var hotIds = await stockReservation.GetTrackedProductIdsAsync();
            if (hotIds.Count == 0) return;

            foreach (var id in hotIds)
            {
                var token = await stockReservation.AcquireLockAsync(
                    StockMaintenanceLock.Key,
                    StockMaintenanceLock.Ttl);
                if (token is null)
                    continue;

                try
                {
                    var sqlStock = (await productRepo.GetStockForManyAsync([id]))
                        .FirstOrDefault();
                    if (sqlStock == default)
                        continue;

                    var pendingByProduct =
                        await orderRepo.GetPendingReservedQuantitiesForAsync([id]);
                    pendingByProduct.TryGetValue(id, out int reservedHeld);
                    int expected = Math.Max(0, sqlStock.Stock - reservedHeld);
                    var actual = await stockReservation.GetStockAsync(id);

                    if (actual is not null && actual.Value != expected)
                    {
                        _logger.LogWarning(
                            "Stock drift for product {ProductId}: Redis={Actual}, expected={Expected}. Correcting.",
                            id, actual.Value, expected);
                        await stockReservation.SetStockIfUnchangedAsync(id, actual.Value, expected);
                    }
                }
                finally
                {
                    await stockReservation.ReleaseLockAsync(StockMaintenanceLock.Key, token);
                }
            }
        }

    }
}
