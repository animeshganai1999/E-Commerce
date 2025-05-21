
using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IOrderedItemService
    {
        Task<List<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId);
        Task<bool> HandleInvoice(Guid userId, byte[] InvoiceBytes, int NumberOfItems, decimal TotalAmount);
        Task SaveInvoiceUrlToDB(Guid userId, string pdfUrl, int NumberOfItems, decimal TotalAmount);
    }
}
