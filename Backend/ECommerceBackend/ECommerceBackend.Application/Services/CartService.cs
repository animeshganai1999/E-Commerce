using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;

namespace ECommerceBackend.Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly ICartCache _cartCache;
        private readonly IProductRepository _productRepository;

        public CartService(
            ICartRepository cartRepository,
            ICartCache cartCache,
            IProductRepository productRepository)
        {
            _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
            _cartCache = cartCache ?? throw new ArgumentNullException(nameof(cartCache));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        }

        public async Task ApplyCartDiffAsync(Guid userId, CartDiffDTO diff)
        {
            var requestedItems = diff.Added.Concat(diff.Updated).ToList();
            if (diff.Added.Concat(diff.Updated).Concat(diff.Removed).Any(item => item.ProductId <= 0))
                throw new ArgumentException("Product identifiers must be greater than zero.");
            if (requestedItems.Any(item => item.Quantity <= 0))
                throw new ArgumentException("Cart quantities must be greater than zero.");

            var duplicateProductId = diff.Added
                .GroupBy(item => item.ProductId)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateProductId.HasValue)
                throw new ArgumentException($"Product {duplicateProductId.Value} appears more than once in Added.");

            var products = (await _productRepository.GetProductsByIdsAsync(diff.Added.Select(item => item.ProductId)))
                .ToDictionary(product => product.Id);

            var missingProductId = diff.Added
                .Select(item => item.ProductId)
                .Cast<int?>()
                .FirstOrDefault(productId => !products.ContainsKey(productId!.Value));
            if (missingProductId.HasValue)
                throw new KeyNotFoundException($"Product {missingProductId.Value} was not found.");

            // Add new items
            var addedItems = diff.Added.Select(item =>
            {
                var product = products[item.ProductId];
                return new CartItem
                {
                    UserId = userId,
                    Description = product.Title,
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                };
            });

            await _cartRepository.AddRangeAsync(addedItems);

            // Update existing items
            foreach (var item in diff.Updated)
            {
                var existing = await _cartRepository.GetAsync(x => x.UserId == userId && x.ProductId == item.ProductId);
                if (existing != null)
                {
                    existing.Quantity = item.Quantity;
                    await _cartRepository.UpdateAsync(existing);
                }
            }

            // Remove items
            foreach (var item in diff.Removed)
            {
                await _cartRepository.DeleteAsync(x => x.UserId == userId && x.ProductId == item.ProductId);
            }

            await _cartRepository.SaveChangesAsync();

            // Invalidate the cached cart so subsequent reads reflect the latest state.
            await _cartCache.InvalidateAsync(userId);
        }

        public async Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId)
        {
            // Cache-aside: serve from Redis on a hit, else load from SQL and populate the cache.
            var cached = await _cartCache.GetByUserAsync(userId);
            if (cached != null)
                return cached;

            var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);

            var items = cartItems.ToList();
            await _cartCache.SetByUserAsync(userId, items);

            return items;
        }
        //public async Task DeleteCartItemsAsync(Guid userId)
        //{
        //    // Fetch the cart items for the given user ID
        //    var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);
        //    if (cartItems == null || !cartItems.Any())
        //        throw new Exception("No items found in the cart.");
        //    // Delete all cart items for the given user ID
        //    foreach (var item in cartItems)
        //    {
        //        await _cartRepository.DeleteAsync(x => x.UserId == userId && x.ProductId == item.ProductId);
        //    }
        //    await _cartRepository.SaveChangesAsync();
        //}
    }
}
