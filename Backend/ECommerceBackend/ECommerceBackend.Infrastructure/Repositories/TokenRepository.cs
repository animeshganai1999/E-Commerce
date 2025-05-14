using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class TokenRepository : Repository<RefreshToken>, ITokenRepository
    {
        private readonly AppDbContext _context;
        public TokenRepository(AppDbContext dbContext) : base(dbContext)
        {
            _context = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }
        // This method retrieves a refresh token by its token string.
        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Token == token);
        }
        // This method retrieves all refresh tokens for a specific user by their user ID.
        public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId)
        {
            return await _context.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToListAsync();
        }

        public async Task<Guid> GetUserIdByTokenAsync(string token)
        {
            var refreshToken = await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Token == token);
            return refreshToken?.UserId ?? Guid.Empty;
        }
    }
}
