namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IIdempotencyRepository
    {
        // Atomically claim the key. true = first time (process), false = already seen.
        Task<bool> TryClaimAsync(string key, TimeSpan ttl);

        // Get the stored state/response ("in-progress", or the cached JSON body).
        Task<string?> GetAsync(string key);

        // Overwrite with the final response once processing completes.
        Task SaveResponseAsync(string key, string response, TimeSpan ttl);

        // Remove the claim (so a failed request can be retried).
        Task RemoveAsync(string key);
    }
}