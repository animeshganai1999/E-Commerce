using ECommerceBackend.Application.Models;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICheckoutService
    {
        Task<byte[]> GenerateInvoiceAsync(InvoiceDataModel model);
        Task<List<OrderItem>> FetchAllIetmsAsync(Guid userId);

        // Generate an invoice PDF for a previously placed order (off the request path).
        Task<byte[]> GenerateInvoiceForOrderAsync(Guid orderId);

        // Payment failed/cancelled: release the reserved stock for the order.
        Task ReleaseStockAsync(Guid orderId);

        // Payment succeeded: confirm the order and enqueue fulfillment via the outbox.
        Task ConfirmStockAsync(Guid orderId);
    }
}
