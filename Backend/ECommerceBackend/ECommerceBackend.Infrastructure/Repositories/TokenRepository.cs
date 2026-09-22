using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class TokenRepository : ITokenRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TokenRepository> _logger;

        public TokenRepository(AppDbContext context, ILogger<TokenRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
        {
            await _context.RefreshTokens.AddAsync(token, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Guid?> RotateAsync(
            string tokenHash,
            string replacementHash,
            DateTime replacementExpiry,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                var familyId = await FindFamilyAsync(tokenHash, cancellationToken);
                if (!familyId.HasValue)
                    return (Guid?)null;

                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                await LockFamilyAsync(familyId.Value, cancellationToken);
                var token = await _context.RefreshTokens.AsNoTracking()
                    .SingleAsync(t => t.TokenHash == tokenHash, cancellationToken);
                var now = DateTime.UtcNow;

                // Check reuse before expiry: even an expired ancestor can reveal a stolen session.
                if (token.IsRevoked)
                {
                    if (token.ReplacedByTokenHash is not null)
                    {
                        await RevokeFamilyTokensAsync(token.FamilyId, now, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        _logger.LogWarning(
                            "Refresh token reuse detected; revoked session {FamilyId} for user {UserId}",
                            token.FamilyId, token.UserId);
                    }
                    return null;
                }

                if (token.ExpiryDate <= now)
                    return null;

                await _context.RefreshTokens.Where(t => t.Id == token.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.IsRevoked, true)
                        .SetProperty(t => t.RevokedAt, now)
                        .SetProperty(t => t.ReplacedByTokenHash, replacementHash), cancellationToken);

                var replacement = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    FamilyId = token.FamilyId,
                    UserId = token.UserId,
                    TokenHash = replacementHash,
                    CreatedAt = now,
                    ExpiryDate = replacementExpiry,
                    UserAgent = userAgent
                };

                try
                {
                    await _context.RefreshTokens.AddAsync(replacement, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                finally
                {
                    // Execution-strategy retries must not reinsert a previously tracked replacement.
                    _context.Entry(replacement).State = EntityState.Detached;
                }

                return (Guid?)token.UserId;
            });
        }

        public async Task RevokeFamilyAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                var familyId = await FindFamilyAsync(tokenHash, cancellationToken);
                if (!familyId.HasValue)
                    return;

                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                await LockFamilyAsync(familyId.Value, cancellationToken);
                await RevokeFamilyTokensAsync(familyId.Value, DateTime.UtcNow, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Revoked refresh session {FamilyId}", familyId.Value);
            });
        }

        private Task<Guid?> FindFamilyAsync(string tokenHash, CancellationToken cancellationToken) =>
            _context.RefreshTokens.AsNoTracking()
                .Where(t => t.TokenHash == tokenHash)
                .Select(t => (Guid?)t.FamilyId)
                .SingleOrDefaultAsync(cancellationToken);

        private async Task LockFamilyAsync(Guid familyId, CancellationToken cancellationToken)
        {
            // Every rotation/revocation locks the root, including operations on different descendants.
            // Keep the root and used tokens until the entire family can be removed.
            await _context.RefreshTokens
                .FromSqlInterpolated(
                    $"SELECT * FROM [RefreshTokens] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {familyId}")
                .AsNoTracking()
                .SingleAsync(cancellationToken);
        }

        private Task<int> RevokeFamilyTokensAsync(
            Guid familyId, DateTime now, CancellationToken cancellationToken) =>
            _context.RefreshTokens
                .Where(t => t.FamilyId == familyId && !t.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsRevoked, true)
                    .SetProperty(t => t.RevokedAt, now), cancellationToken);
    }
}
