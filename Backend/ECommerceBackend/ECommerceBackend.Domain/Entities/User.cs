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
        public required string Email { get; set; } // Display form; uniqueness is enforced on NormalizedEmail

        // Case- and whitespace-insensitive form of Email. A unique index on this column prevents
        // duplicate accounts, and lookups query it so logins are case-insensitive.
        [Required]
        public string NormalizedEmail { get; set; } = string.Empty;

        [Required]
        public required string PasswordHash { get; set; }

        // Authorization role. Defaults to Customer; drives the role claim in the access token.
        // Promote a user to Admin out-of-band (e.g. a DBA UPDATE) — never via a public endpoint.
        [Required]
        public string Role { get; set; } = UserRoles.Customer;

        // Canonical email form used for storage comparisons and lookups.
        // Follows the ASP.NET Identity convention (trim + invariant upper-case).
        public static string NormalizeEmail(string? email) =>
            (email ?? string.Empty).Trim().ToUpperInvariant();
    }
}
