using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICartService
    {
        Task ApplyCartDiffAsync(CartDiffDTO diff);
        Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId);
    }
}
