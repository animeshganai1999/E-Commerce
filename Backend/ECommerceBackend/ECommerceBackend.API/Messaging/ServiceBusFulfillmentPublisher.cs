using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ECommerceBackend.Application.Messaging;
using ECommerceBackend.Application.Options;
using Microsoft.Extensions.Options;

namespace ECommerceBackend.API.Messaging
{
    // Azure Service Bus implementation of IFulfillmentPublisher. Uses a shared ServiceBusClient
    // (registered as a singleton with DefaultAzureCredential — passwordless Entra ID auth).
    public class ServiceBusFulfillmentPublisher : IFulfillmentPublisher
    {
        private readonly ServiceBusSender _sender;

        public ServiceBusFulfillmentPublisher(ServiceBusClient client, IOptions<AzureServiceBusOptions> options)
        {
            _sender = client.CreateSender(options.Value.FulfillmentQueueName);
        }

        public async Task PublishAsync(OrderFulfillmentMessage message, CancellationToken cancellationToken = default)
        {
            var body = JsonSerializer.Serialize(message);
            var sbMessage = new ServiceBusMessage(body)
            {
                ContentType = "application/json",
                // orderId as MessageId gives a natural de-dupe / traceability key.
                MessageId = message.OrderId.ToString()
            };

            await _sender.SendMessageAsync(sbMessage, cancellationToken);
        }
    }
}
