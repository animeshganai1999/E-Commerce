using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICartService
    {
        Task ApplyCartDiffAsync(Guid userId, CartDiffDTO diff);
        Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId);
    }
}
