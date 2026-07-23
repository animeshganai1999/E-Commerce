using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Application.Models;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<Product>> GetProductsAsync(int page, int pageSize, string? category = null);

        // Keyset ("Load more") pagination — scales to deep scrolling without OFFSET cost.
        Task<CursorResult<Product>> GetProductsByCursorAsync(int? afterId, int pageSize, string? category = null);

        Task<Product?> GetProductByIdAsync(int id);
        Task WarmUpProductsAsync(IEnumerable<int> productIds);
    }
}
