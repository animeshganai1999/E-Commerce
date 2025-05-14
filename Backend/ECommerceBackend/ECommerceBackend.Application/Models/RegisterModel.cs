using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.Models
{
    public class RegisterModel
    {
        public required string Name { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        [DataType(DataType.Password)]
        public required string Password { get; set; }
    }
}
