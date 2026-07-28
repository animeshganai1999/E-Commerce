using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;

namespace ECommerceBackend.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IStockReservationRepository _stockReservation;
        private readonly IProductCache _cache;

        // Guard-rails so a caller can't request an unbounded page.
        private const int MaxPageSize = 100;
        private const int DefaultPageSize = 20;

        public ProductService(
            IProductRepository productRepository,
            IStockReservationRepository stockReservation,
            IProductCache cache)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockReservation = stockReservation ?? throw new ArgumentNullException(nameof(stockReservation));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<PagedResult<Product>> GetProductsAsync(int page, int pageSize, string? category = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            // Normalize to lower-case so "Electronics" and "electronics" map to the SAME cache key
            // (and DB filter). SQL Server's default collation is case-insensitive, so the match
            // still uses the Category index.
            category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();

            var cached = await _cache.GetPageAsync(page, pageSize, category);
            if (cached is not null)
                return BuildResult(cached.Value.Items, cached.Value.TotalCount, page, pageSize);

            var (items, totalCount) = await _productRepository.GetProductsPageAsync(page, pageSize, category);
            await _cache.SetPageAsync(page, pageSize, items, totalCount, category);

            return BuildResult(items, totalCount, page, pageSize);
        }

        private static PagedResult<Product> BuildResult(List<Product> items, int totalCount, int page, int pageSize) =>
            new()
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

        public async Task<CursorResult<Product>> GetProductsByCursorAsync(int? afterId, int pageSize, string? category = null)
        {
            if (pageSize < 1) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;
            if (afterId < 0) afterId = null;

            // Same normalization as the offset path so cache keys and DB filters line up.
            category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();

            var cached = await _cache.GetCursorAsync(afterId, pageSize, category);
            if (cached is not null)
            {
                return new CursorResult<Product>
                {
                    Items = cached.Value.Items,
                    NextCursor = cached.Value.NextCursor,
                    PageSize = pageSize
                };
            }

            var (items, nextCursor) = await _productRepository.GetProductsByCursorAsync(afterId, pageSize, category);
            await _cache.SetCursorAsync(afterId, pageSize, items, nextCursor, category);

            return new CursorResult<Product>
            {
                Items = items,
                NextCursor = nextCursor,
                PageSize = pageSize
            };
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            var cached = await _cache.GetByIdAsync(id);
            if (cached is not null)
                return cached;

            var product = await _productRepository.GetProductByIdAsync(id);
            if (product is not null)
                await _cache.SetByIdAsync(product);

            return product;
        }

        public async Task WarmUpProductsAsync(IEnumerable<int> productIds)
        {
            var ids = productIds.ToList();
            var stocks = await _productRepository.GetStockForManyAsync(ids);
            await _stockReservation.WarmUpAsync(stocks);

            // Invalidate cached catalog entries so refreshed data is re-read from SQL.
            await _cache.InvalidateAsync(ids);
        }

        public async Task<int> ResetStockAsync()
        {
            // 1. Wipe all reservation/stock/cache/lock state for a clean baseline.
            await _stockReservation.FlushAllAsync();

            // 2. Re-seed every product's stock:{id} counter from the SQL source of truth.
            var products = (await _productRepository.GetAllProductsAsync()).ToList();
            var stocks = products.Select(p => (p.Id, p.StockQuantity)).ToList();
            await _stockReservation.WarmUpAsync(stocks);

            return stocks.Count;
        }
    }
}
