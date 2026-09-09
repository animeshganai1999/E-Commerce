using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Messaging;
using ECommerceBackend.Application.Options;
using ECommerceBackend.Domain.Entities;
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
                MaxConcurrentCalls = _options.MaxConcurrentCalls,
                PrefetchCount = _options.PrefetchCount,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(
                    _options.MaxAutoLockRenewalMinutes),
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
                    await args.DeadLetterMessageAsync(
                        args.Message,
                        "InvalidPayload",
                        "Body could not be deserialized.",
                        args.CancellationToken);
                    return;
                }

                await FulfillAsync(message.OrderId, args.CancellationToken);

                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fulfillment failed for message {MessageId}", args.Message.MessageId);
                await args.AbandonMessageAsync(
                    args.Message,
                    cancellationToken: args.CancellationToken);
            }
        }

        private async Task FulfillAsync(Guid orderId, CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var stockReservation = scope.ServiceProvider.GetRequiredService<IStockReservationRepository>();
            var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var orderedItemService = scope.ServiceProvider.GetRequiredService<IOrderedItemService>();

            var lockKey = $"lock:fulfillment:{orderId}";
            var lockToken = await stockReservation.AcquireLockAsync(
                lockKey,
                TimeSpan.FromMinutes(_options.MaxAutoLockRenewalMinutes));
            if (lockToken is null)
                throw new InvalidOperationException($"Order {orderId} is already being fulfilled.");

            try
            {
                var order = await orderRepo.GetByIdAsync(orderId);
                if (order == null)
                    throw new InvalidOperationException($"Order {orderId} was not found.");
                if (order.Status != OrderStatus.Confirmed)
                    throw new InvalidOperationException($"Order {orderId} is not confirmed.");

                var invoice = await orderedItemService.GetOrCreateInvoiceAsync(
                    orderId,
                    order.UserId,
                    order.Items.Sum(item => item.Quantity),
                    order.TotalAmount,
                    cancellationToken);
                if (invoice.FulfilledAt.HasValue)
                    return;

                await orderedItemService.MarkAttemptStartedAsync(
                    invoice,
                    DateTime.UtcNow,
                    cancellationToken);

                try
                {
                    byte[]? pdfBytes = null;
                    if (!invoice.BlobUploadedAt.HasValue || !invoice.EmailDispatchedAt.HasValue)
                        pdfBytes = await checkoutService.GenerateInvoiceForOrderAsync(orderId);

                    if (!invoice.BlobUploadedAt.HasValue)
                    {
                        await orderedItemService.UploadInvoiceAsync(
                            invoice,
                            pdfBytes!,
                            DateTime.UtcNow,
                            cancellationToken);
                    }

                    if (!invoice.EmailDispatchedAt.HasValue)
                    {
                        if (!string.IsNullOrWhiteSpace(order.Email))
                        {
                            await emailService.SendInvoiceEmailAsync(
                                _config,
                                pdfBytes!,
                                order.Email,
                                cancellationToken);
                        }

                        await orderedItemService.MarkEmailDispatchedAsync(
                            invoice,
                            DateTime.UtcNow,
                            cancellationToken);
                    }

                    await orderedItemService.MarkFulfilledAsync(
                        invoice,
                        DateTime.UtcNow,
                        cancellationToken);
                    _logger.LogInformation("Fulfillment complete for order {OrderId}", orderId);
                }
                catch (Exception ex)
                {
                    await orderedItemService.RecordFailureAsync(
                        invoice,
                        ex.Message,
                        DateTime.UtcNow,
                        cancellationToken);
                    throw;
                }
            }
            finally
            {
                await stockReservation.ReleaseLockAsync(lockKey, lockToken);
            }
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
