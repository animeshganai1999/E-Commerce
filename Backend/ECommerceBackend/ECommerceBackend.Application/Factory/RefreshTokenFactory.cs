using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Factory
{
    public static class RefreshTokenFactory
    {
        public static RefreshToken Create(Guid userId, string token, DateTime expiryDate, string? userAgent = null)
        {
            return new RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiryDate = expiryDate,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow,
                RevokedAt = null,
                ReplacedByToken = null,
                UserAgent = userAgent
            };
        }
    }
}
