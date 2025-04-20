using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.DTOs
{
    public class UserDTO
    {
        [ValidateNever]
        public int Id { get; set; }
        [Required(ErrorMessage = "Email is required")]
        public required string Email { get; set; }
        [Required(ErrorMessage = "Password is required")]
        public required string PasswordHash { get; set; }
    }
}
