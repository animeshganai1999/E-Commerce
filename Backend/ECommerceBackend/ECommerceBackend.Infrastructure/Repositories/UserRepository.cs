using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        private readonly AppDbContext _context;
        public UserRepository(AppDbContext dbContext) : base(dbContext) {
            _context = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        // Retrieves a user by email, matching on the normalized form so lookups are
        // case- and whitespace-insensitive.
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var normalized = User.NormalizeEmail(email);
            return await _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
        }
        public async Task<User?> GetUserByUserIdAsync(Guid? userId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
