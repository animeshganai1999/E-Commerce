using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;

namespace ECommerceBackend.Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        public CartService(ICartRepository cartRepository)
        {
            _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
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
        }

        public async Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId)
        {
            // Fetch the cart items for the given user ID
            var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);

            // Return the fetched cart items
            return cartItems;
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
