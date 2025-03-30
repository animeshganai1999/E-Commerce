using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICartRepository : IRepository<CartItem>
    {
        Task<IEnumerable<CartItem>> GetCartByUserIdAsync(int userId);
    }
}
