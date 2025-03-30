using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class CartRepository : Repository<CartItem>, ICartRepository
    {
        private readonly AppDbContext _context;
        public CartRepository(AppDbContext dbContext) : base(dbContext)
        {
            _context = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        // This method retrieves all cart items for a specific user by their user ID.
        public async Task<IEnumerable<CartItem>> GetCartByUserIdAsync(int userId)
        {
            return await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
        }
    }
}
