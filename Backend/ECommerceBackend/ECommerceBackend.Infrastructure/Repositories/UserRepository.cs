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

        // This method retrieves a user by their email address.
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }
        public async Task<User?> GetUserByUserIdAsync(Guid? userId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
