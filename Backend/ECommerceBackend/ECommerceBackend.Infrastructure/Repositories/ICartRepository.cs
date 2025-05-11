using ECommerceBackend.Domain.Entities;
using System.Linq.Expressions;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface ICartRepository : IRepository<CartItem>
    {
        Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId);
        Task AddRangeAsync(IEnumerable<CartItem> entities);
        Task<CartItem?> GetAsync(Expression<Func<CartItem, bool>> filter);
        Task SaveChangesAsync();
    }
}
