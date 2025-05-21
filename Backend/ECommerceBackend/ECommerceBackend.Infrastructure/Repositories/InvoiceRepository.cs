using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class InvoiceRepository : Repository<UserInvoice>, IInvoiceRepository
    {
        private readonly AppDbContext _context;
        public InvoiceRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
        public async Task<IEnumerable<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId)
        {
            return await _context.UserInvoice
                .Where(invoice => invoice.UserId == userId)
                .ToListAsync();
        }
    }
}
