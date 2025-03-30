using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("CartItems")]
    public class CartItem
    {
        [Required]
        public int Id { get; set; } // This is the primary key for the CartItem entity. (Auto-incremented)
        [Required]
        public int UserId { get; set; } // This is a foreign key that references the User entity.
        [Required]
        public required int ProductId { get; set; } // This is the ID of the product in the cart.
        [Required]
        public int Quantity { get; set; } // This is the quantity of the product in the cart.
    }
}
