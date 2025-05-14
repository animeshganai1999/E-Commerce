using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("RefreshTokens")]
    public class RefreshToken
    {
        public Guid Id { get; set; } // Primary key (GUID)
        public Guid UserId { get; set; } // Foreign key to the User entity
        public required string Token { get; set; } // The refresh token string
        public DateTime ExpiryDate { get; set; } // Expiry date of the refresh token
        public bool IsRevoked { get; set; } // Indicates if the token has been revoked
        public DateTime CreatedAt { get; set; } // The date and time when the token was created
        public DateTime? RevokedAt { get; set; } // The date and time when the token was revoked (nullable)
        public string? ReplacedByToken { get; set; } // The token that replaced this one (nullable)
        public string? UserAgent { get; set; } // The user agent string of the client that created the token (nullable)
    }
}
