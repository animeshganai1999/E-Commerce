namespace ECommerceBackend.Application.Messaging
{
    // The message published to Service Bus after stock is settled, carrying the slow
    // fulfillment work (invoice PDF + email + persist) to a background worker.
    public record OrderFulfillmentMessage(Guid OrderId);
}
