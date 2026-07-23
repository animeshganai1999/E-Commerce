using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Messaging;
using ECommerceBackend.Application.Options;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace ECommerceBackend.API.HostedServices
{
    // Consumes OrderFulfillmentMessage from Azure Service Bus and performs the SLOW work
    // off the request/relay path: generate invoice PDF, email it, persist the invoice record.
    //
    // Service Bus gives independent retries + automatic dead-lettering (after MaxDeliveryCount),
    // so a flaky email provider no longer blocks stock settlement or the checkout response.
    public class FulfillmentWorker : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly IServiceProvider _services;
        private readonly ILogger<FulfillmentWorker> _logger;
        private readonly IConfiguration _config;
        private readonly AzureServiceBusOptions _options;
        private ServiceBusProcessor? _processor;

        public FulfillmentWorker(
            ServiceBusClient client,
            IServiceProvider services,
            ILogger<FulfillmentWorker> logger,
            IConfiguration config,
            IOptions<AzureServiceBusOptions> options)
        {
            _client = client;
            _services = services;
            _logger = logger;
            _config = config;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _processor = _client.CreateProcessor(_options.FulfillmentQueueName, new ServiceBusProcessorOptions
            {
                // Process one at a time; let Service Bus handle retries/DLQ on failure.
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false
            });

            _processor.ProcessMessageAsync += HandleMessageAsync;
            _processor.ProcessErrorAsync += HandleErrorAsync;

            await _processor.StartProcessingAsync(stoppingToken);
        }

        private async Task HandleMessageAsync(ProcessMessageEventArgs args)
        {
            try
            {
                var message = JsonSerializer.Deserialize<OrderFulfillmentMessage>(args.Message.Body.ToString());
                if (message is null)
                {
                    // Unparseable — dead-letter immediately (no point retrying).
                    await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Body could not be deserialized.");
                    return;
                }

                await FulfillAsync(message.OrderId);

                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fulfillment failed for message {MessageId}", args.Message.MessageId);
                // Abandon -> Service Bus redelivers; after MaxDeliveryCount it auto dead-letters.
                await args.AbandonMessageAsync(args.Message);
            }
        }

        private async Task FulfillAsync(Guid orderId)
        {
            using var scope = _services.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var orderedItemService = scope.ServiceProvider.GetRequiredService<IOrderedItemService>();

            var order = await orderRepo.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Fulfillment: order {OrderId} not found; skipping.", orderId);
                return;
            }

            // 1. Generate the invoice PDF (slow work).
            var pdfBytes = await checkoutService.GenerateInvoiceForOrderAsync(orderId);

            // 2. Email the invoice.
            if (!string.IsNullOrWhiteSpace(order.Email))
                await emailService.SendEmailAsync(_config, pdfBytes: pdfBytes, ReceiverEmail: order.Email);

            // 3. Persist the invoice record.
            await orderedItemService.HandleInvoice(
                order.UserId, pdfBytes, order.Items.Count, order.TotalAmount + 30);

            _logger.LogInformation("Fulfillment complete for order {OrderId}", orderId);
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus processor error ({Source})", args.ErrorSource);
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_processor is not null)
            {
                await _processor.StopProcessingAsync(cancellationToken);
                await _processor.DisposeAsync();
            }
            await base.StopAsync(cancellationToken);
        }
    }
}
