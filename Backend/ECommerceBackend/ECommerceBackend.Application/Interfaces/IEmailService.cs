using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Models;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IEmailService
    {
        Task<(bool isSuccess, string errorMessage)> SendEmailAsync(IConfiguration config, ContactRequestModel? request = null, byte[]? pdfBytes = null, string? ReceiverEmail = null);
    }
}
