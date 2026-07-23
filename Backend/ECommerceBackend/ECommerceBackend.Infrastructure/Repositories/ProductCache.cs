using ECommerceBackend.Domain.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace ECommerceBackend.Infrastructure.Repositories
{
    // Redis-backed product read cache using the shared IConnectionMultiplexer — consistent with
    // StockReservationRepository (single Redis programming model across the codebase).
    public class ProductCache : IProductCache
    {
        private readonly IDatabase _db;

        // Short TTL: only catalog data (title/price/description) is cached. Live stock correctness
        // is still governed by the Redis reservation counters + SQL source of truth.
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

        // Page keys self-expire via TTL, so no index/registry is needed (avoids an unbounded set).
        // A category ("all" when none) is part of the key so each filter has its own cached pages.
        private static string PageKey(int page, int pageSize, string? category) =>
            $"cache:products:page:{(string.IsNullOrWhiteSpace(category) ? "all" : category)}:{page}:{pageSize}";
        private static string IdKey(int id) => $"cache:products:{id}";

        // Cursor keys use the "afterId" (or "start" for the first batch) instead of a page number.
        private static string CursorKey(int? afterId, int pageSize, string? category) =>
            $"cache:products:cursor:{(string.IsNullOrWhiteSpace(category) ? "all" : category)}:{(afterId?.ToString() ?? "start")}:{pageSize}";

        public ProductCache(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        private sealed record PageEnvelope(List<Product> Items, int TotalCount);

        public async Task<(List<Product> Items, int TotalCount)?> GetPageAsync(int page, int pageSize, string? category = null)
        {
            var value = await _db.StringGetAsync(PageKey(page, pageSize, category));
            if (value.IsNullOrEmpty) return null;

            var envelope = JsonSerializer.Deserialize<PageEnvelope>(value!);
            return envelope is null ? null : (envelope.Items, envelope.TotalCount);
        }

        public async Task SetPageAsync(int page, int pageSize, IEnumerable<Product> items, int totalCount, string? category = null)
        {
            var payload = JsonSerializer.Serialize(new PageEnvelope(items.ToList(), totalCount));
            await _db.StringSetAsync(PageKey(page, pageSize, category), payload, Ttl);
        }

        private sealed record CursorEnvelope(List<Product> Items, int? NextCursor);

        public async Task<(List<Product> Items, int? NextCursor)?> GetCursorAsync(int? afterId, int pageSize, string? category = null)
        {
            var value = await _db.StringGetAsync(CursorKey(afterId, pageSize, category));
            if (value.IsNullOrEmpty) return null;

            var envelope = JsonSerializer.Deserialize<CursorEnvelope>(value!);
            return envelope is null ? null : (envelope.Items, envelope.NextCursor);
        }

        public async Task SetCursorAsync(int? afterId, int pageSize, IEnumerable<Product> items, int? nextCursor, string? category = null)
        {
            var payload = JsonSerializer.Serialize(new CursorEnvelope(items.ToList(), nextCursor));
            await _db.StringSetAsync(CursorKey(afterId, pageSize, category), payload, Ttl);
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            var value = await _db.StringGetAsync(IdKey(id));
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Product>(value!);
        }

        public async Task SetByIdAsync(Product product)
        {
            await _db.StringSetAsync(IdKey(product.Id), JsonSerializer.Serialize(product), Ttl);
        }

        public async Task InvalidateAsync(IEnumerable<int> productIds)
        {
            // Clear the affected per-id entries immediately. Cached pages are left to expire via
            // their short TTL (a rare warm-up doesn't justify tracking every page key).
            var keys = productIds.Select(id => (RedisKey)IdKey(id)).ToArray();
            if (keys.Length > 0)
                await _db.KeyDeleteAsync(keys);
        }
    }
}
