using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    // Cache-aside read cache for the (rarely changing) product catalog.
    // Backed by the same Redis instance used for reservations/locks (via IConnectionMultiplexer).
    public interface IProductCache
    {
        // Per-page caching keeps each Redis value small (scales to millions of products).
        Task<(List<Product> Items, int TotalCount)?> GetPageAsync(int page, int pageSize, string? category = null);
        Task SetPageAsync(int page, int pageSize, IEnumerable<Product> items, int totalCount, string? category = null);

        Task<Product?> GetByIdAsync(int id);
        Task SetByIdAsync(Product product);

        // Invalidate cached catalog entries (e.g. after a warm-up / stock refresh).
        Task InvalidateAsync(IEnumerable<int> productIds);
    }
}
