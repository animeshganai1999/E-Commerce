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

        public async Task<StockSettlementResult> SettleStockAsync(Guid orderId, DateTime settledAt)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order is null)
                    return StockSettlementResult.OrderNotFound;
                if (order.Status != OrderStatus.Confirmed)
                    return StockSettlementResult.OrderNotConfirmed;
                if (order.StockSettledAt.HasValue)
                    return StockSettlementResult.AlreadySettled;

                foreach (var item in order.Items)
                {
                    var rowsAffected = await _context.Products
                        .Where(product =>
                            product.Id == item.ProductId
                            && product.StockQuantity >= item.Quantity)
                        .ExecuteUpdateAsync(setters =>
                            setters.SetProperty(
                                product => product.StockQuantity,
                                product => product.StockQuantity - item.Quantity));

                    if (rowsAffected != 1)
                    {
                        await transaction.RollbackAsync();
                        throw new InvalidOperationException(
                            $"Unable to settle stock for product {item.ProductId} in order {orderId}.");
                    }
                }

                order.StockSettledAt = settledAt;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return StockSettlementResult.Settled;
            });
        }

        public async Task<OrderTransitionResult> ConfirmWithOutboxAsync(
            Guid orderId,
            Guid userId,
            DateTime confirmedAt,
            OutboxMessage outboxMessage)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return OrderTransitionResult.NotFound;
            if (order.UserId != userId)
                return OrderTransitionResult.Forbidden;

            try
            {
                var transition = order.TryConfirm(confirmedAt);
                if (transition == OrderTransitionResult.Expired)
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return OrderTransitionResult.Expired;
                }
                if (transition != OrderTransitionResult.Succeeded)
                    return transition;

                await _context.OutboxMessages.AddAsync(outboxMessage);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return OrderTransitionResult.Succeeded;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                var current = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                return current?.Status switch
                {
                    OrderStatus.Confirmed => OrderTransitionResult.AlreadyConfirmed,
                    OrderStatus.Failed or OrderStatus.Cancelled => OrderTransitionResult.AlreadyFailed,
                    _ => throw new DbUpdateConcurrencyException(
                        $"Order {orderId} changed during confirmation.")
                };
            }
        }

        public async Task<ExpiredReservationAction> ResolveExpiredReservationAsync(
            Guid orderId,
            DateTime asOfUtc)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is null)
                return ExpiredReservationAction.Release;
            if (order.Status == OrderStatus.Confirmed)
                return ExpiredReservationAction.Confirm;
            if (order.Status is OrderStatus.Failed or OrderStatus.Cancelled)
                return ExpiredReservationAction.Release;
            if (order.ReservationExpiresAt > asOfUtc)
                return ExpiredReservationAction.Skip;

            var transition = order.TryFail();
            if (transition != OrderTransitionResult.Succeeded)
            {
                return transition switch
                {
                    OrderTransitionResult.AlreadyConfirmed => ExpiredReservationAction.Confirm,
                    OrderTransitionResult.AlreadyFailed => ExpiredReservationAction.Release,
                    _ => ExpiredReservationAction.Skip
                };
            }

            try
            {
                await _context.SaveChangesAsync();
                return ExpiredReservationAction.Release;
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                var current = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                return current?.Status switch
                {
                    null => ExpiredReservationAction.Release,
                    OrderStatus.Confirmed => ExpiredReservationAction.Confirm,
                    OrderStatus.Failed or OrderStatus.Cancelled => ExpiredReservationAction.Release,
                    _ => ExpiredReservationAction.Skip
                };
            }
        }

        public async Task<OrderTransitionResult> FailAsync(Guid orderId, Guid userId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);

            if (order is null)
                return OrderTransitionResult.NotFound;
            if (order.UserId != userId)
                return OrderTransitionResult.Forbidden;
            var transition = order.TryFail();
            if (transition != OrderTransitionResult.Succeeded)
                return transition;

            try
            {
                await _context.SaveChangesAsync();
                return OrderTransitionResult.Succeeded;
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                var current = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                return current?.Status switch
                {
                    OrderStatus.Confirmed => OrderTransitionResult.AlreadyConfirmed,
                    OrderStatus.Failed or OrderStatus.Cancelled => OrderTransitionResult.AlreadyFailed,
                    _ => throw new DbUpdateConcurrencyException(
                        $"Order {orderId} changed while being failed.")
                };
            }
        }

        // Sum quantities still represented by Redis reservations. Confirmed orders remain
        // included until StockSettledAt is written in the same stock-maintenance critical section.
        public async Task<Dictionary<int, int>> GetPendingReservedQuantitiesAsync()
        {
            return await _context.OrderItems
                .Where(i => i.Order.StockSettledAt == null
                            && (i.Order.Status == OrderStatus.Pending
                                || i.Order.Status == OrderStatus.Confirmed))
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
                .Where(i => i.Order.StockSettledAt == null
                            && (i.Order.Status == OrderStatus.Pending
                                || i.Order.Status == OrderStatus.Confirmed)
                            && ids.Contains(i.ProductId))
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
