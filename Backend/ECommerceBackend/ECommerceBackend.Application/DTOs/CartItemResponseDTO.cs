namespace ECommerceBackend.Application.DTOs
{
    public class CartItemResponseDTO
    {
        public Guid UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
