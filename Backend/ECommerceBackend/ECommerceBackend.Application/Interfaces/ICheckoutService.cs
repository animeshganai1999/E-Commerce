using ECommerceBackend.Application.Models;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICheckoutService
    {
        Task<byte[]> GenerateInvoiceAsync(InvoiceDataModel model);
        Task<List<OrderItem>> FetchAllIetmsAsync(Guid userId);
    }
}
