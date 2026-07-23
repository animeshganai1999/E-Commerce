namespace ECommerceBackend.Application.Messaging
{
    // Abstraction over the message transport (Azure Service Bus). Keeps the outbox relay
    // decoupled from the concrete SDK and easy to test.
    public interface IFulfillmentPublisher
    {
        // Publishes an order-fulfillment message. The messageId is used for de-duplication
        // and traceability (the orderId makes a natural idempotency key).
        Task PublishAsync(OrderFulfillmentMessage message, CancellationToken cancellationToken = default);
    }
}
