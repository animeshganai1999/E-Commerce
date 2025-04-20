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

        //// This will add or update the product in the cart.
        //public async Task<bool> AddUserProductAsync(int userId, int productId, int quantity)
        //{
        //    if (quantity <= 0)
        //    {
        //        return false; // Invalid quantity, return false
        //    }

        //    var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);
        //    var existingItem = cartItems.FirstOrDefault(c => c.ProductId == productId);

        //    if (existingItem != null)
        //    {
        //        // Update quantity if product already exists in the cart
        //        existingItem.Quantity = quantity;
        //        await _cartRepository.UpdateAsync(existingItem);
        //    }
        //    else
        //    {
        //        // Add new product to the cart
        //        var newItem = new CartItem
        //        {
        //            UserId = userId,
        //            ProductId = productId,
        //            Quantity = quantity
        //        };
        //        await _cartRepository.AddAsync(newItem);
        //    }

        //    return true; // Operation successful
        //}

        //// Delete the cart item from the database, if the quantity become 0, delete the item from the cart
        //public async Task<bool> DeleteUserProductAsync(int userId, int productId)
        //{
        //    var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);
        //    var itemToDelete = cartItems.FirstOrDefault(c => c.ProductId == productId);
        //    if (itemToDelete != null)
        //    {
        //        await _cartRepository.DeleteAsync(c => c.Id == itemToDelete.Id);
        //        return true;
        //    }
        //    return false;
        //}

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

        public async Task<IEnumerable<CartItem>> GetCartByUserIdAsync(int userId)
        {
            // Fetch the cart items for the given user ID
            var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);

            // Return the fetched cart items
            return cartItems;
        }
    }
}
