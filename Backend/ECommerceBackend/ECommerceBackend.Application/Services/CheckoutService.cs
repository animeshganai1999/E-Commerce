using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Application.Constants;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using QuestPDF.Elements;
using ECommerceBackend.Infrastructure.Repositories;
using ECommerceBackend.Domain.Entities;
using System.Text.Json;

namespace ECommerceBackend.Application.Services
{
    public class CheckoutService : ICheckoutService
    {
        // How long a reservation is held in Redis while the user pays (mirrored on the order).
        private static readonly TimeSpan ReservationWindow = TimeSpan.FromMinutes(15);

        ICartRepository _cartRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IStockReservationRepository _stockReservation;
        private readonly IProductRepository _productRepository;
        private readonly ICartCache _cartCache;

        public CheckoutService(
            ICartRepository cartRepository,
            IOrderRepository orderRepository,
            IStockReservationRepository stockReservation,
            IProductRepository productRepository,
            ICartCache cartCache)
        {
            _cartRepository = cartRepository;
            _orderRepository = orderRepository;
            _stockReservation = stockReservation;
            _productRepository = productRepository;
            _cartCache = cartCache;
        }

        public async Task<BeginCheckoutResult> BeginCheckoutAsync(Guid userId, BeginCheckoutModel model)
        {
            // 1. Load the user's cart (source of the items to reserve + price snapshot).
            var cartItems = (await _cartRepository.GetCartByUserIdAsync(userId)).ToList();
            if (cartItems.Count == 0)
                throw new Exception("No items found in the cart.");
            if (cartItems.Count > CartPolicy.MaxDistinctItems)
            {
                throw new RequestValidationException(
                    nameof(CartItem),
                    $"A cart can contain at most {CartPolicy.MaxDistinctItems} distinct products.");
            }
            if (cartItems.Any(item => item.Quantity is <= 0 or > CartPolicy.MaxQuantityPerItem))
            {
                throw new RequestValidationException(
                    nameof(CartItem.Quantity),
                    $"Quantity must be between 1 and {CartPolicy.MaxQuantityPerItem}.");
            }

            var products = (await _productRepository.GetProductsByIdsAsync(
                    cartItems.Select(item => item.ProductId)))
                .ToDictionary(product => product.Id);

            var missingProductId = cartItems
                .Select(item => item.ProductId)
                .Cast<int?>()
                .FirstOrDefault(productId => !products.ContainsKey(productId!.Value));
            if (missingProductId.HasValue)
            {
                throw new RequestValidationException(
                    nameof(CartItem.ProductId),
                    $"Product {missingProductId.Value} was not found.");
            }

            var orderId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var expiresAt = now.Add(ReservationWindow);
            var reserved = new List<CartItem>();
            var details = model.OrderDetails;
            var order = new Order
            {
                Id = orderId,
                UserId = userId,
                Status = OrderStatus.Pending,
                CreatedAt = now,
                ReservationExpiresAt = expiresAt,
                TotalAmount = cartItems.Sum(item => products[item.ProductId].Price * item.Quantity),
                FirstName = details.FirstName,
                LastName = details.LastName,
                Email = details.Email,
                Address = details.Address,
                Address2 = details.Address2,
                Country = details.Country,
                State = details.State,
                Zip = details.Zip,
                Items = cartItems.Select(i => new OrderLineItem
                {
                    OrderId = orderId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = products[i.ProductId].Price,
                    Description = products[i.ProductId].Title
                }).ToList()
            };

            // Reconciliation must not observe the Redis reservation without its matching SQL
            // order. The shared maintenance lock covers only this consistency boundary.
            var lockToken = await AcquireStockMaintenanceLockAsync();
            try
            {
                try
                {
                    // 2. Reserve each line in Redis (the hot path). Lazy-load the stock key from SQL
                    //    on a miss, then retry once so cold/evicted products still reserve correctly.
                    foreach (var item in cartItems)
                    {
                        var result = await _stockReservation.TryReserveAsync(
                            orderId, item.ProductId, item.Quantity, ReservationWindow);

                        if (result == ReserveResult.StockMissing)
                        {
                            var sqlStock = await _productRepository.GetStockFromSqlAsync(item.ProductId);
                            if (sqlStock.HasValue)
                            {
                                await _stockReservation.PopulateStockIfAbsentAsync(item.ProductId, sqlStock.Value);
                                result = await _stockReservation.TryReserveAsync(
                                    orderId, item.ProductId, item.Quantity, ReservationWindow);
                            }
                        }

                        if (result != ReserveResult.Success)
                            throw new InsufficientStockException(item.ProductId, item.Quantity);

                        reserved.Add(item);
                    }

                    // 3. Persist the Pending order while reconciliation is excluded.
                    await _orderRepository.AddAsync(order);
                    await _orderRepository.SaveChangesAsync();
                }
                catch
                {
                    foreach (var item in reserved)
                        await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);
                    throw;
                }
            }
            finally
            {
                await _stockReservation.ReleaseLockAsync(StockMaintenanceLock.Key, lockToken);
            }

            // 4. Clear the ordered items from the cart so a subsequent checkout doesn't
            //    re-order the same items. Invalidate the cached cart to match SQL.
            foreach (var item in cartItems)
                await _cartRepository.DeleteAsync(x => x.UserId == userId && x.ProductId == item.ProductId);
            await _cartRepository.SaveChangesAsync();
            await _cartCache.InvalidateAsync(userId);

            return new BeginCheckoutResult
            {
                OrderId = orderId,
                TotalAmount = order.TotalAmount,
                ReservationExpiresAt = expiresAt
            };
        }

        public async Task<byte[]> GenerateInvoiceForOrderAsync(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception($"Order {orderId} not found.");

            var orderItems = order.Items.Select(item => new OrderItem
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.Quantity * item.UnitPrice
            }).ToList();

            var details = new OrderDetails
            {
                FirstName = order.FirstName ?? string.Empty,
                LastName = order.LastName ?? string.Empty,
                Email = order.Email ?? string.Empty,
                Address = order.Address ?? string.Empty,
                Address2 = order.Address2 ?? string.Empty,
                Country = order.Country ?? string.Empty,
                State = order.State ?? string.Empty,
                Zip = order.Zip ?? string.Empty
            };

            return BuildInvoicePdf(details, orderItems);
        }

        public async Task ReleaseStockAsync(Guid orderId, Guid userId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException($"Order {orderId} was not found.");
            if (order.UserId != userId)
                throw new ForbiddenAccessException("You are not authorized to modify this order.");

            var lockToken = await AcquireStockMaintenanceLockAsync();
            try
            {
                var result = await _orderRepository.FailAsync(orderId, userId);
                if (result == OrderTransitionResult.AlreadyConfirmed)
                    throw new OrderStateConflictException("A confirmed order cannot be failed.");
                if (result == OrderTransitionResult.Forbidden)
                    throw new ForbiddenAccessException("You are not authorized to modify this order.");
                if (result == OrderTransitionResult.NotFound)
                    throw new KeyNotFoundException($"Order {orderId} was not found.");

                foreach (var item in order.Items)
                    await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);
            }
            finally
            {
                await _stockReservation.ReleaseLockAsync(StockMaintenanceLock.Key, lockToken);
            }
        }

        public async Task ConfirmStockAsync(Guid orderId, Guid userId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException($"Order {orderId} was not found.");
            if (order.UserId != userId)
                throw new ForbiddenAccessException("You are not authorized to modify this order.");
            if (order.Status == OrderStatus.Confirmed)
                return;
            if (order.Status is OrderStatus.Failed or OrderStatus.Cancelled)
                throw new OrderStateConflictException("A failed order cannot be confirmed.");

            // Enqueue fulfillment (invoice + email + stock settle) via the outbox.
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateId = orderId,
                Type = "OrderConfirmed",
                Payload = JsonSerializer.Serialize(new { OrderId = orderId }),
                CreatedAt = DateTime.UtcNow
            };

            var lockToken = await AcquireStockMaintenanceLockAsync();

            try
            {
                var confirmedAt = DateTime.UtcNow;
                var hasAllReservations = true;
                foreach (var item in order.Items)
                {
                    if (!await _stockReservation.ReservationExistsAsync(orderId, item.ProductId))
                    {
                        hasAllReservations = false;
                        break;
                    }
                }

                if (!hasAllReservations)
                {
                    var failureResult = await _orderRepository.FailAsync(orderId, userId);
                    if (failureResult == OrderTransitionResult.AlreadyConfirmed)
                        return;

                    foreach (var item in order.Items)
                        await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);

                    throw new OrderStateConflictException(
                        "The stock reservation is no longer available.");
                }

                var result = await _orderRepository.ConfirmWithOutboxAsync(
                    orderId,
                    userId,
                    confirmedAt,
                    outboxMessage);

                if (result == OrderTransitionResult.AlreadyConfirmed)
                    return;
                if (result == OrderTransitionResult.Forbidden)
                    throw new ForbiddenAccessException("You are not authorized to modify this order.");
                if (result == OrderTransitionResult.NotFound)
                    throw new KeyNotFoundException($"Order {orderId} was not found.");
                if (result is OrderTransitionResult.AlreadyFailed or OrderTransitionResult.Expired)
                {
                    foreach (var item in order.Items)
                        await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);

                    throw new OrderStateConflictException(
                        result == OrderTransitionResult.Expired
                            ? "The stock reservation has expired."
                            : "A failed order cannot be confirmed.");
                }
            }
            finally
            {
                await _stockReservation.ReleaseLockAsync(StockMaintenanceLock.Key, lockToken);
            }
        }

        private async Task<string> AcquireStockMaintenanceLockAsync()
        {
            const int attempts = 100;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var token = await _stockReservation.AcquireLockAsync(
                    StockMaintenanceLock.Key,
                    StockMaintenanceLock.Ttl);
                if (token is not null)
                    return token;

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new OrderStateConflictException(
                "Inventory maintenance is in progress. Retry the payment request.");
        }

        private static byte[] BuildInvoicePdf(OrderDetails details, List<OrderItem> orderItems)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, details, orderItems));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Thank you for your purchase!");
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col => // Updated from RelativeColumn to RelativeItem
                {
                    col.Item().Text("E-Commerce Invoice").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                    col.Item().Text($"Invoice Date: {DateTime.Now:dd MMM yyyy}");
                });

                row.ConstantItem(100).Height(50).Placeholder(); // logo or blank
            });
        }

        private static void ComposeContent(IContainer container, OrderDetails details, List<OrderItem> OrderItems)
        {
            container.PaddingVertical(10).Column(col =>
            {
                col.Item().Element(c => ComposeCustomerDetails(c, details));
                col.Item().PaddingTop(15).Element(c => ComposeTable(c, OrderItems));
                col.Item().PaddingTop(10).AlignRight().Text($"Total Amount: ₹ {OrderItems.Sum(i => i.TotalPrice) + 30}").Bold().FontSize(14);
            });
        }

        private static void ComposeCustomerDetails(IContainer container, OrderDetails details)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col => // Updated from RelativeColumn to RelativeItem
                {
                    col.Item().Text("Billing Information").Bold().Underline();
                    col.Item().Text($"{details.FirstName} {details.LastName}");
                    col.Item().Text(details.Email);
                    col.Item().Text(details.Address);
                    if (!string.IsNullOrWhiteSpace(details.Address2))
                        col.Item().Text(details.Address2);
                    col.Item().Text($"{details.State}, {details.Zip}, {details.Country}");
                });
            });
        }

        private static void ComposeTable(IContainer container, List<OrderItem> items)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Description
                    columns.RelativeColumn(1); // Qty
                    columns.RelativeColumn(1); // Unit Price
                    columns.RelativeColumn(1); // Total
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("Description").SemiBold();
                    header.Cell().Element(CellStyle).AlignCenter().Text("Qty").SemiBold();
                    header.Cell().Element(CellStyle).AlignRight().Text("Unit Price").SemiBold();
                    header.Cell().Element(CellStyle).AlignRight().Text("Total").SemiBold();

                    static IContainer CellStyle(IContainer container)
                    {
                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                    }
                });

                foreach (var item in items)
                {
                    table.Cell().Element(CellStyle).Text(item.Description);
                    table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString());
                    table.Cell().Element(CellStyle).AlignRight().Text($"₹ {item.UnitPrice}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"₹ {item.TotalPrice}");

                    static IContainer CellStyle(IContainer container)
                    {
                        return container.PaddingVertical(5);
                    }
                }
            });
        }
    }
}
