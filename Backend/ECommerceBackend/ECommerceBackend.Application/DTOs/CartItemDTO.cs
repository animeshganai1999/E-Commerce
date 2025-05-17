namespace ECommerceBackend.Application.DTOs
{
    public class CartItemDTO
    {
        public string? Description { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public Guid UserId { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
