using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        private readonly AppDbContext _context;
        public ProductRepository(AppDbContext dbContext) : base(dbContext)
        {
            _context = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        // Atomic conditional UPDATE: the database serializes concurrent orders, so two
        // buyers cannot both deduct beyond the available stock (prevents oversell).
        public async Task<bool> TryDeductStockAsync(int productId, int quantity)
        {
            int rowsAffected = await _context.Products
                .Where(p => p.Id == productId && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity));

            return rowsAffected > 0;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _context.Products.AsNoTracking().ToListAsync();
        }

        public async Task<(List<Product> Items, int TotalCount)> GetProductsPageAsync(int page, int pageSize, string? category = null)
        {
            IQueryable<Product> query = _context.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            query = query.OrderBy(p => p.Id);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        // Keyset (seek) pagination: seeks past the cursor via the index instead of
        // OFFSET-counting skipped rows, so performance stays constant at any depth.
        public async Task<(List<Product> Items, int? NextCursor)> GetProductsByCursorAsync(int? afterId, int pageSize, string? category = null)
        {
            IQueryable<Product> query = _context.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            if (afterId.HasValue)
                query = query.Where(p => p.Id > afterId.Value); // the seek

            // Fetch one extra row to detect whether a further batch exists.
            var rows = await query
                .OrderBy(p => p.Id)
                .Take(pageSize + 1)
                .ToListAsync();

            int? nextCursor = null;
            if (rows.Count > pageSize)
            {
                rows.RemoveAt(pageSize);          // drop the probe row
                nextCursor = rows[^1].Id;         // last returned item's Id = next cursor
            }

            return (rows, nextCursor);
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        }
        public async Task<int?> GetStockFromSqlAsync(int productId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => (int?)p.StockQuantity)
                .FirstOrDefaultAsync();
        }
        public async Task<List<(int Id, int Stock)>> GetStockForManyAsync(IEnumerable<int> productIds)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new ValueTuple<int, int>(p.Id, p.StockQuantity))
                .ToListAsync();
        }
    }
}
