using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.Models
{
    public class LoginModel
    {
        [EmailAddress]
        public required string Email { get; set; }
        [DataType(DataType.Password)]
        public required string Password { get; set; }
    }
}
