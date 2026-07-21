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
        public CartService(ICartRepository cartRepository, ICartCache cartCache)
        {
            _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
            _cartCache = cartCache ?? throw new ArgumentNullException(nameof(cartCache));
        }

        // Fix for CS8601: Possible null reference assignment.
        // The issue is that `item.Description` in `CartDiffDTO` might be null, but `CartItem.Description` is marked as required.
        // To fix this, we can use the null-coalescing operator to provide a default value if `item.Description` is null.

        public async Task ApplyCartDiffAsync(CartDiffDTO diff)
        {
            // Add new items
            var addedItems = diff.Added.Select(item => new CartItem
            {
                UserId = diff.UserId,
                Description = item.Description ?? string.Empty,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });

            await _cartRepository.AddRangeAsync(addedItems);

            // Update existing items
            foreach (var item in diff.Updated)
            {
                var existing = await _cartRepository.GetAsync(x => x.UserId == diff.UserId && x.ProductId == item.ProductId);
                if (existing != null)
                {
                    existing.Quantity = item.Quantity;
                    await _cartRepository.UpdateAsync(existing);
                }
            }

            // Remove items
            foreach (var item in diff.Removed)
            {
                await _cartRepository.DeleteAsync(x => x.UserId == diff.UserId && x.ProductId == item.ProductId);
            }

            await _cartRepository.SaveChangesAsync();

            // Invalidate the cached cart so subsequent reads reflect the latest state.
            await _cartCache.InvalidateAsync(diff.UserId);
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
