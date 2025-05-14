using ECommerceBackend.Domain.Entities;
using System.Linq.Expressions;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface ITokenRepository : IRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId);
        Task AddAsync(RefreshToken entity);
        Task AddRangeAsync(IEnumerable<RefreshToken> entities);
        Task<RefreshToken?> GetAsync(Expression<Func<RefreshToken, bool>> filter);
        Task SaveChangesAsync();
        Task<Guid> GetUserIdByTokenAsync(string refreshToken);
    }
}
