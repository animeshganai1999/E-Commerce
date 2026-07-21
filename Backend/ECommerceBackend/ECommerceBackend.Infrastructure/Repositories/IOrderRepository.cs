using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order);
        Task<Order?> GetByIdAsync(Guid orderId);       // includes line items
        Task UpdateStatusAsync(Guid orderId, OrderStatus status, DateTime? confirmedAt = null);
        Task MarkStockSettledAsync(Guid orderId, DateTime settledAt);

        // Reconciliation helpers.
        // Total quantity currently held by Pending (unsettled) orders, grouped by product.
        Task<Dictionary<int, int>> GetPendingReservedQuantitiesAsync();
        // Same, but filtered to a specific set of product ids (scales to large catalogs).
        Task<Dictionary<int, int>> GetPendingReservedQuantitiesForAsync(IEnumerable<int> productIds);
        // Pending orders whose reservation has expired (for cleanup).
        Task<List<Order>> GetExpiredPendingOrdersAsync(DateTime asOfUtc);

        Task SaveChangesAsync();
    }
}
