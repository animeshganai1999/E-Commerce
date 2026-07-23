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

        /// <summary>
        /// Keyset (seek) pagination for "Load more" experiences. Returns up to
        /// <paramref name="pageSize"/> products whose Id is greater than
        /// <paramref name="afterId"/> (or from the start when afterId is null),
        /// plus the cursor (last Id) to request the next batch. Scales to deep
        /// pagination because it seeks via the (Category, Id) index instead of
        /// counting through skipped rows.
        /// </summary>
        Task<(List<Product> Items, int? NextCursor)> GetProductsByCursorAsync(int? afterId, int pageSize, string? category = null);

        /// <summary>Returns a single product by id (read-only, no tracking), or null.</summary>
        Task<Product?> GetProductByIdAsync(int id);
        Task<int?> GetStockFromSqlAsync(int productId);
        Task<List<(int Id, int Stock)>> GetStockForManyAsync(IEnumerable<int> productIds);
    }
}
