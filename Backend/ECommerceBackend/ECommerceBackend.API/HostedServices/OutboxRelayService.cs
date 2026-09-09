using ECommerceBackend.Application.Messaging;
using ECommerceBackend.Application.Constants;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using System.Text.Json;

namespace ECommerceBackend.API.HostedServices
{
    // Outbox RELAY: reliably reads confirmed-order outbox messages, settles stock inline
    // (fast + critical), then PUBLISHES a fulfillment message to Azure Service Bus. The slow
    // work (invoice PDF + email + persist) is handled off this path by FulfillmentWorker.
    //
    // Outbox = atomic capture (written in the same SQL txn as Order=Confirmed).
    // Service Bus = reliable transport with independent retries + dead-lettering.
    public class OutboxRelayService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OutboxRelayService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

        public OutboxRelayService(IServiceProvider services, ILogger<OutboxRelayService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            const string outboxLockKey = "lock:outbox-processor";
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    var stockReservation = scope.ServiceProvider.GetRequiredService<IStockReservationRepository>();
                    var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IFulfillmentPublisher>();

                    // Only one instance relays the outbox per cycle (multi-instance safe).
                    var lockToken = await stockReservation.AcquireLockAsync(
                        outboxLockKey,
                        TimeSpan.FromMinutes(5));
                    if (lockToken is null)
                    {
                        await Task.Delay(_interval, stoppingToken);
                        continue; // another instance is processing this cycle
                    }

                    try
                    {
                        var messages = await outbox.GetReadyAsync(
                            batchSize: 20,
                            DateTime.UtcNow);

                        foreach (var msg in messages)
                        {
                            try
                            {
                                if (msg.Type == "OrderConfirmed")
                                {
                                    await SettleAndPublishAsync(
                                        msg,
                                        stockReservation,
                                        orderRepo,
                                        publisher,
                                        stoppingToken);
                                }
                                else
                                {
                                    throw new InvalidOperationException(
                                        $"Unsupported outbox message type '{msg.Type}'.");
                                }

                                await outbox.MarkPublishedAsync(msg.Id, DateTime.UtcNow);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                await outbox.RecordFailureAsync(msg.Id, ex.Message, DateTime.UtcNow);
                                _logger.LogError(ex, "Outbox relay message {Id} failed (retry {Retry})", msg.Id, msg.RetryCount + 1);
                            }
                        }
                    }
                    finally
                    {
                        await stockReservation.ReleaseLockAsync(outboxLockKey, lockToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox relay loop failed");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private static async Task SettleAndPublishAsync(
            OutboxMessage message,
            IStockReservationRepository stockReservation,
            IOrderRepository orderRepo,
            IFulfillmentPublisher publisher,
            CancellationToken cancellationToken)
        {
            var orderId = message.AggregateId
                ?? JsonSerializer.Deserialize<OrderConfirmedPayload>(message.Payload)?.OrderId
                ?? throw new InvalidOperationException(
                    $"Outbox message {message.Id} does not contain an order id.");
            var order = await orderRepo.GetByIdAsync(orderId);
            if (order == null)
                throw new InvalidOperationException($"Order {orderId} was not found.");

            // Keep only Redis/SQL stock mutations under the shared maintenance lock. Service Bus
            // publication happens afterward so a slow external call cannot block checkouts.
            var stockLockToken = await stockReservation.AcquireLockAsync(
                StockMaintenanceLock.Key,
                StockMaintenanceLock.Ttl);
            if (stockLockToken is null)
                throw new InvalidOperationException("Stock maintenance is busy.");

            try
            {
                order = await orderRepo.GetByIdAsync(orderId);
                if (order == null)
                    throw new InvalidOperationException($"Order {orderId} was not found.");

                var settlement = await orderRepo.SettleStockAsync(orderId, DateTime.UtcNow);
                if (settlement == StockSettlementResult.OrderNotConfirmed)
                    throw new InvalidOperationException($"Order {orderId} is not confirmed.");
                if (settlement == StockSettlementResult.OrderNotFound)
                    throw new InvalidOperationException($"Order {orderId} was not found.");

                foreach (var item in order.Items)
                {
                    await stockReservation.ConfirmAsync(
                        orderId,
                        item.ProductId,
                        item.Quantity);
                }
            }
            finally
            {
                await stockReservation.ReleaseLockAsync(StockMaintenanceLock.Key, stockLockToken);
            }

            // Always retry publication even when stock was settled by an earlier attempt.
            await publisher.PublishAsync(new OrderFulfillmentMessage(orderId), cancellationToken);
        }

        private record OrderConfirmedPayload(Guid OrderId);
    }
}
