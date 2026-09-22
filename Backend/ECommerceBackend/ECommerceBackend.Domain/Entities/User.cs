using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ECommerceBackend.Domain.Constants;

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

        // Authorization role. Defaults to Customer; drives the role claim in the access token.
        // Promote a user to Admin out-of-band (e.g. a DBA UPDATE) — never via a public endpoint.
        [Required]
        public string Role { get; set; } = UserRoles.Customer;
    }
}
