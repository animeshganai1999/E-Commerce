namespace ECommerceBackend.Application.DTOs
{
    public class CartItemDTO
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public Guid UserId { get; set; }
    }
}
