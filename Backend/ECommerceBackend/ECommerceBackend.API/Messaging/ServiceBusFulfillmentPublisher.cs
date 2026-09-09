using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ECommerceBackend.Application.Messaging;
using ECommerceBackend.Application.Options;
using Microsoft.Extensions.Options;

namespace ECommerceBackend.API.Messaging
{
    // Azure Service Bus implementation of IFulfillmentPublisher. Uses a shared ServiceBusClient
    // (registered as a singleton with DefaultAzureCredential � passwordless Entra ID auth).
    public class ServiceBusFulfillmentPublisher : IFulfillmentPublisher, IAsyncDisposable
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
                MessageId = message.OrderId.ToString(),
                CorrelationId = message.OrderId.ToString(),
                Subject = nameof(OrderFulfillmentMessage)
            };
            sbMessage.ApplicationProperties["OrderId"] = message.OrderId.ToString();
            sbMessage.ApplicationProperties["EventType"] = nameof(OrderFulfillmentMessage);

            await _sender.SendMessageAsync(sbMessage, cancellationToken);
        }

        public ValueTask DisposeAsync() => _sender.DisposeAsync();
    }
}
