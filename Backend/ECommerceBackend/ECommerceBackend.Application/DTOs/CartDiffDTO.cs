namespace ECommerceBackend.Application.DTOs
{
    public class CartDiffDTO
    {
        public Guid UserId { get; set; }
        public List<CartItemDTO> Added { get; set; }
        public List<CartItemDTO> Updated { get; set; }
        public List<CartItemDTO> Removed { get; set; }
    }
}
