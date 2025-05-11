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

        // This will apply the cart diff to the database.
        public async Task ApplyCartDiffAsync(CartDiffDTO diff)
        {
            // Add new items
            var addedItems = diff.Added.Select(item => new CartItem
            {
                UserId = diff.UserId,
                ProductId = item.ProductId,
                Quantity = item.Quantity
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
    }
}
