using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.DTOs
{
    public class CartItemDTO
    {
        [ValidateNever]
        public int Id { get; set; } // This is the primary key for the CartItem entity. (Auto-incremented)
        [Required(ErrorMessage = "User Id is required")]
        public int UserId { get; set; } // This is a foreign key that references the User entity.
        [Required(ErrorMessage = "Product Id is required")]
        public required int ProductId { get; set; } // This is the ID of the product in the cart.
        [Required(ErrorMessage = "Product quantity is required")]
        public int Quantity { get; set; } // This is the quantity of the product in the cart.
    }
}
