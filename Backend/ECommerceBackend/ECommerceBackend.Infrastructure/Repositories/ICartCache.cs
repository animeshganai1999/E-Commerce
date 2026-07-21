using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    // Cache-aside read cache for a user's cart. Backed by the same Redis instance used for
    // reservations/locks (via IConnectionMultiplexer). Per-user key, invalidated on every write.
    public interface ICartCache
    {
        Task<List<CartItem>?> GetByUserAsync(Guid userId);
        Task SetByUserAsync(Guid userId, IEnumerable<CartItem> items);

        // Invalidate a user's cached cart (call after any add/update/remove).
        Task InvalidateAsync(Guid userId);
    }
}
