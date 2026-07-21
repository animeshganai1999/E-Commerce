using ECommerceBackend.Infrastructure.Repositories;

public class ReservationSweeperService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationSweeperService> _logger;

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _lockTtl = TimeSpan.FromSeconds(25); // < interval
    private const string LockKey = "lock:reservation-sweeper";

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

                // Only ONE instance across the cluster wins the lock this cycle.
                var token = await repo.AcquireLockAsync(LockKey, _lockTtl);

                if (token is not null)
                {
                    try
                    {
                        int reclaimed = await repo.ReclaimExpiredAsync();
                        if (reclaimed > 0)
                            _logger.LogInformation(
                                "Reservation sweeper reclaimed {Count} expired reservations",
                                reclaimed);
                    }
                    finally
                    {
                        // Always release our lock (safe compare-and-delete).
                        await repo.ReleaseLockAsync(LockKey, token);
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