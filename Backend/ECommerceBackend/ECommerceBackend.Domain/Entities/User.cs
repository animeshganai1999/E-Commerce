using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("Users")]
    public class User
    {
        [Required]
        public int Id { get; set; } // This is the primary key for the User entity. (Auto-incremented)
        [Required]
        [EmailAddress]
        public required string Email { get; set; } // This is the email address of the user. (Unique)
        [Required]
        public required string PasswordHash { get; set; } // This is the hashed password of the user.
    }
}
