using ECommerceBackend.Domain.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace ECommerceBackend.Infrastructure.Repositories
{
    // Redis-backed cart read cache using the shared IConnectionMultiplexer — consistent with
    // ProductCache / StockReservationRepository (single Redis programming model across the codebase).
    public class CartCache : ICartCache
    {
        private readonly IDatabase _db;

        // Short TTL is a safety net; correctness comes from explicit invalidation on every write.
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        private static string CartKey(Guid userId) => $"cache:cart:{userId}";

        public CartCache(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task<List<CartItem>?> GetByUserAsync(Guid userId)
        {
            var value = await _db.StringGetAsync(CartKey(userId));
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<List<CartItem>>(value!);
        }

        public async Task SetByUserAsync(Guid userId, IEnumerable<CartItem> items)
        {
            await _db.StringSetAsync(CartKey(userId), JsonSerializer.Serialize(items.ToList()), Ttl);
        }

        public async Task InvalidateAsync(Guid userId)
        {
            await _db.KeyDeleteAsync(CartKey(userId));
        }
    }
}
