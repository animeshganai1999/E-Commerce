namespace ECommerceBackend.Application.Models
{
    public class InvoiceDataModel
    {
        public Guid UserId { get; set; } // User ID associated with the invoice
        public required OrderDetails OrderDetails { get; set; }
    }
}
