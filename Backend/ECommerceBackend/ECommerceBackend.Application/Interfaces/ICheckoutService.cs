using ECommerceBackend.Application.Models;

namespace ECommerceBackend.Application.Interfaces
{
    public interface ICheckoutService
    {
        // Step 1 of checkout: reserve stock in Redis + create a Pending order (with a billing
        // snapshot). Throws InsufficientStockException if any item cannot be reserved.
        Task<BeginCheckoutResult> BeginCheckoutAsync(BeginCheckoutModel model);

        // Generate an invoice PDF for a previously placed order (off the request path).
        Task<byte[]> GenerateInvoiceForOrderAsync(Guid orderId);

        // Payment failed/cancelled: release the reserved stock for the order.
        Task ReleaseStockAsync(Guid orderId);

        // Payment succeeded: confirm the order and enqueue fulfillment via the outbox.
        Task ConfirmStockAsync(Guid orderId);
    }
}
