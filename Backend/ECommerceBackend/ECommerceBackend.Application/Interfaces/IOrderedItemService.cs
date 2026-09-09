
using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IOrderedItemService
    {
        Task<List<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId);
        Task<UserInvoice> GetOrCreateInvoiceAsync(
            Guid orderId,
            Guid userId,
            int numberOfItems,
            decimal totalAmount,
            CancellationToken cancellationToken);
        Task MarkAttemptStartedAsync(UserInvoice invoice, DateTime attemptedAt, CancellationToken cancellationToken);
        Task UploadInvoiceAsync(
            UserInvoice invoice,
            byte[] invoiceBytes,
            DateTime uploadedAt,
            CancellationToken cancellationToken);
        Task MarkEmailDispatchedAsync(
            UserInvoice invoice,
            DateTime dispatchedAt,
            CancellationToken cancellationToken);
        Task MarkFulfilledAsync(
            UserInvoice invoice,
            DateTime fulfilledAt,
            CancellationToken cancellationToken);
        Task RecordFailureAsync(
            UserInvoice invoice,
            string error,
            DateTime attemptedAt,
            CancellationToken cancellationToken);
    }
}
