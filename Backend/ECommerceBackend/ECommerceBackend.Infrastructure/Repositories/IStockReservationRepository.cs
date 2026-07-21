namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IStockReservationRepository
    {
        Task PreloadStockAsync(int productId, int quantity);

        // Dual write: DECRBY stock + SET functional key (TTL) + ZADD tracker — atomically.
        Task<ReserveResult> TryReserveAsync(Guid orderId, int productId, int quantity, TimeSpan ttl);

        // Payment success: remove functional key + tracker so sweeper won't reclaim.
        Task ConfirmAsync(Guid orderId, int productId, int quantity);

        // Explicit release (payment failure/cancel): INCRBY stock + cleanup.
        Task ReleaseAsync(Guid orderId, int productId, int quantity);

        // Reclaim expired reservations (no locking here — the caller coordinates).
        Task<int> ReclaimExpiredAsync();

        // --- Generic distributed lock (reusable, e.g. sweeper + outbox processor) ---
        Task<string?> AcquireLockAsync(string lockKey, TimeSpan ttl);
        Task ReleaseLockAsync(string lockKey, string token);

        // Populate a single stock key only if absent (lazy-load safe).
        Task PopulateStockIfAbsentAsync(int productId, int quantity, TimeSpan? idleTtl = null);

        // Force-set stock keys for a bulk warm-up (overwrites — deliberate refresh).
        Task WarmUpAsync(IEnumerable<(int Id, int Stock)> items);

        // Read the current Redis stock value for a product (null if the key is absent).
        Task<long?> GetStockAsync(int productId);

        // Force-set the authoritative reconciled value for a product's stock key.
        Task SetStockAsync(int productId, int quantity);

        // Product ids that currently have a stock key in Redis (the "hot" set) — via SCAN.
        Task<List<int>> GetTrackedProductIdsAsync();
    }
}