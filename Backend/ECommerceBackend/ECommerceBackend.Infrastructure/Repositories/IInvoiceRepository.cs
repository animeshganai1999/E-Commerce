using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IInvoiceRepository : IRepository<UserInvoice>
    {
        Task<IEnumerable<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId);
        Task SaveChangesAsync();
    }
}
