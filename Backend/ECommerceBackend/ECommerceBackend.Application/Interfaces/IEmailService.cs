using ECommerceBackend.Application.DTOs;
namespace ECommerceBackend.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendInvoiceEmailAsync(
            IConfiguration config,
            byte[] pdfBytes,
            string receiverEmail);
    }
}
