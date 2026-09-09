using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.DTOs
{
    public class CartItemDTO
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        public int Quantity { get; set; }
    }
}
