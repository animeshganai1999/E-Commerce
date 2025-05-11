using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("Users")]
    public class User
    {
        [Key]
        public Guid UserId { get; set; } // Primary key (GUID)

        [Required]
        public required string Name { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; } // Email should be unique (add uniqueness in DB config)

        [Required]
        public required string PasswordHash { get; set; }
    }
}
