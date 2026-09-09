using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IInvoiceRepository : IRepository<UserInvoice>
    {
        Task<IEnumerable<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId);
        Task<UserInvoice?> GetByOrderIdAsync(Guid orderId);
        Task<UserInvoice> GetOrCreateAsync(UserInvoice invoice);
        Task SaveChangesAsync();
    }
}
