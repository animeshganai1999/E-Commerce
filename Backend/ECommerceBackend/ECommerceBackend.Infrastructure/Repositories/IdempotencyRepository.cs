using StackExchange.Redis;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class IdempotencyRepository : IIdempotencyRepository
    {
        private readonly IDatabase _db;
        private const string InProgress = "in_progress";
        public IdempotencyRepository(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }
        public async Task<bool> TryClaimAsync(string key, TimeSpan ttl)
        {
            // SET idempotency:{key} "in-progress" NX EX ttl
            return await _db.StringSetAsync(
                Key(key), InProgress, expiry: ttl, when: When.NotExists);
        }

        public async Task<string?> GetAsync(string key)
        {
            var val = await _db.StringGetAsync(Key(key));
            return val.HasValue ? val.ToString() : null;
        }

        public async Task SaveResponseAsync(string key, string response, TimeSpan ttl)
        {
            await _db.StringSetAsync(Key(key), response, expiry: ttl);
        }

        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(Key(key));
        }

        private static string Key(string key) => $"idempotency:{key}";
    }
}
