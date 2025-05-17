using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using QuestPDF.Elements;
using ECommerceBackend.Infrastructure.Repositories;
using ECommerceBackend.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace ECommerceBackend.Application.Services
{
    public class CheckoutService : ICheckoutService
    {
        ICartRepository _cartRepository;
        public CheckoutService(ICartRepository cartRepository)
        {
            _cartRepository = cartRepository;
        }
        private async Task<List<OrderItem>> FetchAllIetmsAsync(Guid userId)
        {
            // Fetch the cart items for the given user ID
            var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);
            if (cartItems == null || !cartItems.Any())
                throw new Exception("No items found in the cart.");
            // Map the cart items to OrderItem
            var orderItems = cartItems.Select(item => new OrderItem
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.Quantity * item.UnitPrice
            }).ToList();
            return orderItems;
        }
        public async Task<byte[]> GenerateInvoiceAsync(InvoiceDataModel model)
        {
            // Fetch the order items asynchronously
            List<OrderItem> orderItems = await FetchAllIetmsAsync(model.UserId);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, model, orderItems));
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

        private static void ComposeContent(IContainer container, InvoiceDataModel model, List<OrderItem> OrderItems)
        {
            container.PaddingVertical(10).Column(col =>
            {
                col.Item().Element(c => ComposeCustomerDetails(c, model.OrderDetails));
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
