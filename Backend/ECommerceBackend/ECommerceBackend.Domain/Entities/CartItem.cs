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
        public Guid UserId { get; set; } // This is a foreign key that references the User entity.
        public required string Description { get; set; } // This is a description of the product in the cart.
        [Required]
        public int ProductId { get; set; } // This is the ID of the product in the cart.
        public required decimal UnitPrice { get; set; } // This is the unit price of the product.
        [Required]
        public int Quantity { get; set; } // This is the quantity of the product in the cart.
    }
}
