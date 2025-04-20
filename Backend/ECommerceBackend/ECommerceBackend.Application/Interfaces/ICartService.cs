using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICartService
    {
        //Task<bool> DeleteUserProductAsync(int userId, int productId);
        //Task<bool> AddUserProductAsync(int userId, int productId, int quantity);
        Task ApplyCartDiffAsync(CartDiffDTO diff);
        Task<IEnumerable<CartItem>> GetCartByUserIdAsync(int userId);
    }
}
