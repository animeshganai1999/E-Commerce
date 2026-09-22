using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface ITokenRepository
    {
        Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
        Task<Guid?> RotateAsync(
            string tokenHash,
            string replacementHash,
            DateTime replacementExpiry,
            string? userAgent,
            CancellationToken cancellationToken = default);
        Task RevokeFamilyAsync(string tokenHash, CancellationToken cancellationToken = default);
    }
}
