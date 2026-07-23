using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using QuestPDF.Elements;
using ECommerceBackend.Infrastructure.Repositories;
using ECommerceBackend.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
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
        private readonly IOutboxRepository _outboxRepository;
        private readonly IProductRepository _productRepository;

        public CheckoutService(
            ICartRepository cartRepository,
            IOrderRepository orderRepository,
            IStockReservationRepository stockReservation,
            IOutboxRepository outboxRepository,
            IProductRepository productRepository)
        {
            _cartRepository = cartRepository;
            _orderRepository = orderRepository;
            _stockReservation = stockReservation;
            _outboxRepository = outboxRepository;
            _productRepository = productRepository;
        }

        public async Task<BeginCheckoutResult> BeginCheckoutAsync(BeginCheckoutModel model)
        {
            // 1. Load the user's cart (source of the items to reserve + price snapshot).
            var cartItems = (await _cartRepository.GetCartByUserIdAsync(model.UserId)).ToList();
            if (cartItems.Count == 0)
                throw new Exception("No items found in the cart.");

            var orderId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var expiresAt = now.Add(ReservationWindow);
            var reserved = new List<CartItem>();

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
            }
            catch
            {
                // 3. Roll back any reservations already taken so stock isn't leaked on a partial fail.
                foreach (var item in reserved)
                    await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);
                throw;
            }

            // 4. Persist a Pending order with a billing snapshot (used later by the fulfillment worker).
            var details = model.OrderDetails;
            var order = new Order
            {
                Id = orderId,
                UserId = model.UserId,
                Status = OrderStatus.Pending,
                CreatedAt = now,
                ReservationExpiresAt = expiresAt,
                TotalAmount = cartItems.Sum(i => i.UnitPrice * i.Quantity),
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
                    UnitPrice = i.UnitPrice,
                    Description = i.Description
                }).ToList()
            };

            try
            {
                await _orderRepository.AddAsync(order);
                await _orderRepository.SaveChangesAsync();
            }
            catch
            {
                // Order persistence failed after reserving — release the held stock.
                foreach (var item in reserved)
                    await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);
                throw;
            }

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

        public async Task ReleaseStockAsync(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            foreach (var item in order.Items)
                await _stockReservation.ReleaseAsync(orderId, item.ProductId, item.Quantity);

            await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Failed);
            await _orderRepository.SaveChangesAsync();
        }

        public async Task ConfirmStockAsync(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return;

            await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Confirmed, DateTime.UtcNow);

            // Enqueue fulfillment (invoice + email + stock settle) via the outbox.
            await _outboxRepository.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "OrderConfirmed",
                Payload = JsonSerializer.Serialize(new { OrderId = orderId }),
                CreatedAt = DateTime.UtcNow
            });

            await _orderRepository.SaveChangesAsync();
            await _outboxRepository.SaveChangesAsync();
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
