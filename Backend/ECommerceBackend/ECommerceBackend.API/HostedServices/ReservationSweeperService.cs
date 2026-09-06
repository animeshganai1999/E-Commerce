using ECommerceBackend.Infrastructure.Repositories;
using ECommerceBackend.Application.Constants;

public class ReservationSweeperService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationSweeperService> _logger;

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public ReservationSweeperService(
        IServiceProvider services,
        ILogger<ReservationSweeperService> logger)
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
                // Repo is Scoped, so open a scope per cycle (singleton -> scoped rule).
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider
                                .GetRequiredService<IStockReservationRepository>();
                var orderRepository = scope.ServiceProvider
                                .GetRequiredService<IOrderRepository>();

                // Only ONE instance across the cluster wins the lock this cycle.
                var token = await repo.AcquireLockAsync(
                    StockMaintenanceLock.Key,
                    StockMaintenanceLock.Ttl);

                if (token is not null)
                {
                    try
                    {
                        var asOfUtc = DateTime.UtcNow;
                        int reclaimed = await repo.ReclaimExpiredAsync(
                            orderId => orderRepository.ResolveExpiredReservationAsync(orderId, asOfUtc));
                        if (reclaimed > 0)
                            _logger.LogInformation(
                                "Reservation sweeper reclaimed {Count} expired reservations",
                                reclaimed);
                    }
                    finally
                    {
                        // Always release our lock (safe compare-and-delete).
                        await repo.ReleaseLockAsync(
                            StockMaintenanceLock.Key,
                            token);
                    }
                }
                // else: another instance holds the lock — skip this cycle.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservation sweeper failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}