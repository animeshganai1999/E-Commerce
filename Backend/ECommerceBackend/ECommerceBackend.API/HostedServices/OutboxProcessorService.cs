using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using System.Text.Json;

namespace ECommerceBackend.API.HostedServices
{
    // Reliably processes outbox messages: settles stock (Redis + SQL) AND performs the slow
    // fulfillment work (invoice PDF + email) OFF the request path. Retries on failure.
    public class OutboxProcessorService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OutboxProcessorService> _logger;
        private readonly IConfiguration _config;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

        public OutboxProcessorService(IServiceProvider services, ILogger<OutboxProcessorService> logger, IConfiguration config)
        {
            _services = services;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            const string lockKey = "lock:outbox-processor";
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    var stockReservation = scope.ServiceProvider.GetRequiredService<IStockReservationRepository>();
                    var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                    var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                    var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var orderedItemService = scope.ServiceProvider.GetRequiredService<IOrderedItemService>();

                    // Only one instance processes the outbox per cycle (multi-instance safe).
                    var lockToken = await stockReservation.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(25));
                    if (lockToken is null)
                    {
                        await Task.Delay(_interval, stoppingToken);
                        continue; // another instance is processing this cycle
                    }

                    try
                    {
                        var messages = await outbox.GetUnprocessedAsync(batchSize: 20);

                        foreach (var msg in messages)
                        {
                            try
                            {
                                if (msg.Type == "OrderConfirmed")
                                    await FulfillOrderAsync(msg.Payload, stockReservation, productRepo,
                                        orderRepo, checkoutService, emailService, orderedItemService);

                                await outbox.MarkProcessedAsync(msg.Id);
                            }
                            catch (Exception ex)
                            {
                                await outbox.MarkFailedAsync(msg.Id, ex.Message);
                                _logger.LogError(ex, "Outbox message {Id} failed (retry {Retry})", msg.Id, msg.RetryCount + 1);
                            }
                        }
                    }
                    finally
                    {
                        await stockReservation.ReleaseLockAsync(lockKey, lockToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox processor loop failed");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task FulfillOrderAsync(
            string payload,
            IStockReservationRepository stockReservation,
            IProductRepository productRepo,
            IOrderRepository orderRepo,
            ICheckoutService checkoutService,
            IEmailService emailService,
            IOrderedItemService orderedItemService)
        {
            var orderId = JsonSerializer.Deserialize<OrderConfirmedPayload>(payload)!.OrderId;
            var order = await orderRepo.GetByIdAsync(orderId);
            if (order == null) return;

            // Idempotency: skip if this order was already fulfilled/settled.
            if (order.StockSettledAt != null) return;

            // 1. Settle stock: finalize Redis reservation + deduct SQL (source of truth).
            foreach (var item in order.Items)
            {
                await stockReservation.ConfirmAsync(orderId, item.ProductId, item.Quantity);
                await productRepo.TryDeductStockAsync(item.ProductId, item.Quantity);
            }

            // 2. Generate the invoice PDF (slow work — now OFF the request path).
            var pdfBytes = await checkoutService.GenerateInvoiceForOrderAsync(orderId);

            // 3. Email the invoice.
            if (!string.IsNullOrWhiteSpace(order.Email))
                await emailService.SendEmailAsync(_config, pdfBytes: pdfBytes, ReceiverEmail: order.Email);

            // 4. Persist the invoice record.
            await orderedItemService.HandleInvoice(
                order.UserId, pdfBytes, order.Items.Count, order.TotalAmount + 30);

            // 5. Mark settled (idempotency guard).
            await orderRepo.MarkStockSettledAsync(orderId, DateTime.UtcNow);
        }

        private record OrderConfirmedPayload(Guid OrderId);
    }
}
