using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        public OrderRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddAsync(Order order)
        {
            await _context.Orders.AddAsync(order);
        }

        public async Task<Order?> GetByIdAsync(Guid orderId)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task UpdateStatusAsync(Guid orderId, OrderStatus status, DateTime? confirmedAt = null)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return;

            order.Status = status;
            if (confirmedAt.HasValue)
                order.ConfirmedAt = confirmedAt;
        }

        public async Task MarkStockSettledAsync(Guid orderId, DateTime settledAt)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return;

            order.StockSettledAt = settledAt;
            await _context.SaveChangesAsync();
        }

        // Sum of quantities held by Pending (unsettled) orders, grouped by ProductId.
        public async Task<Dictionary<int, int>> GetPendingReservedQuantitiesAsync()
        {
            return await _context.OrderItems
                .Where(i => i.Order.Status == OrderStatus.Pending)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Qty);
        }

        // Same, but only for the given product ids (scales to large catalogs).
        public async Task<Dictionary<int, int>> GetPendingReservedQuantitiesForAsync(IEnumerable<int> productIds)
        {
            var ids = productIds.ToList();
            if (ids.Count == 0) return new Dictionary<int, int>();

            return await _context.OrderItems
                .Where(i => i.Order.Status == OrderStatus.Pending && ids.Contains(i.ProductId))
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Qty);
        }

        public async Task<List<Order>> GetExpiredPendingOrdersAsync(DateTime asOfUtc)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Pending
                            && o.ReservationExpiresAt != null
                            && o.ReservationExpiresAt < asOfUtc)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
