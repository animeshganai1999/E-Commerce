namespace ECommerceBackend.Application.Models
{
    public class OrderItem
    {
        public required string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
