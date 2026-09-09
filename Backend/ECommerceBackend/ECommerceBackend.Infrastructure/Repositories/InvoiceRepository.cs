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
                .AsNoTracking()
                .Where(invoice => invoice.UserId == userId)
                .ToListAsync();
        }

        public async Task<UserInvoice?> GetByOrderIdAsync(Guid orderId)
        {
            return await _context.UserInvoice
                .FirstOrDefaultAsync(invoice => invoice.OrderId == orderId);
        }

        public async Task<UserInvoice> GetOrCreateAsync(UserInvoice invoice)
        {
            if (!invoice.OrderId.HasValue)
                throw new ArgumentException("An order id is required.", nameof(invoice));

            var existing = await GetByOrderIdAsync(invoice.OrderId.Value);
            if (existing is not null)
                return existing;

            await _context.UserInvoice.AddAsync(invoice);
            try
            {
                await _context.SaveChangesAsync();
                return invoice;
            }
            catch (DbUpdateException)
            {
                _context.Entry(invoice).State = EntityState.Detached;
                var concurrentInvoice = await GetByOrderIdAsync(invoice.OrderId.Value);
                if (concurrentInvoice is not null)
                    return concurrentInvoice;
                throw;
            }
        }
    }
}
