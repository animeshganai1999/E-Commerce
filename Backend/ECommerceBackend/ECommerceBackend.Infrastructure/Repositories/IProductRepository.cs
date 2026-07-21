using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IProductRepository : IRepository<Product>
    {
        /// <summary>
        /// Atomically deducts stock for a product only if enough is available.
        /// Returns true if the deduction succeeded, false if there was insufficient stock.
        /// </summary>
        Task<bool> TryDeductStockAsync(int productId, int quantity);

        /// <summary>Returns all products (read-only, no tracking).</summary>
        Task<IEnumerable<Product>> GetAllProductsAsync();

        /// <summary>Returns a page of products (read-only, no tracking) plus the total count.
        /// Optionally filtered by category.</summary>
        Task<(List<Product> Items, int TotalCount)> GetProductsPageAsync(int page, int pageSize, string? category = null);

        /// <summary>Returns a single product by id (read-only, no tracking), or null.</summary>
        Task<Product?> GetProductByIdAsync(int id);
        Task<int?> GetStockFromSqlAsync(int productId);
        Task<List<(int Id, int Stock)>> GetStockForManyAsync(IEnumerable<int> productIds);
    }
}
