using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("RefreshTokens")]
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        // The first token's Id identifies the family and its SQL synchronization row.
        public Guid FamilyId { get; set; }
        public required string TokenHash { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByTokenHash { get; set; }
        public string? UserAgent { get; set; }
    }
}
