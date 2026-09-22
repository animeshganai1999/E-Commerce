using ECommerceBackend.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceBackend.Application.Factory
{
    public static class RefreshTokenFactory
    {
        public static RefreshToken Create(Guid userId, string token, DateTime expiryDate, string? userAgent = null)
        {
            var id = Guid.NewGuid();
            return new RefreshToken
            {
                Id = id,
                FamilyId = id,
                UserId = userId,
                TokenHash = Hash(token),
                ExpiryDate = expiryDate,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow,
                RevokedAt = null,
                ReplacedByTokenHash = null,
                UserAgent = userAgent
            };
        }

        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
