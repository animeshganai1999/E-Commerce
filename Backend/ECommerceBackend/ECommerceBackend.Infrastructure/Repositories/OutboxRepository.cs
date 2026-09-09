using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class OutboxRepository : IOutboxRepository
    {
        private readonly AppDbContext _context;
        public OutboxRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddAsync(OutboxMessage message)
        {
            await _context.OutboxMessages.AddAsync(message);
        }

        public async Task<List<OutboxMessage>> GetReadyAsync(int batchSize, DateTime asOfUtc)
        {
            return await _context.OutboxMessages
                .Where(message =>
                    message.ProcessedAt == null
                    && message.FailedAt == null
                    && (message.NextAttemptAt == null || message.NextAttemptAt <= asOfUtc))
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync();
        }

        public async Task MarkPublishedAsync(Guid id, DateTime publishedAt)
        {
            var msg = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (msg == null) return;
            msg.MarkPublished(publishedAt);
            await _context.SaveChangesAsync();
        }

        public async Task RecordFailureAsync(Guid id, string error, DateTime attemptedAt)
        {
            const int maxAutomaticAttempts = 10;
            var msg = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (msg == null) return;
            msg.RecordFailure(error, attemptedAt, maxAutomaticAttempts);
            await _context.SaveChangesAsync();
        }

        public async Task<List<OutboxMessage>> GetFailedAsync(int batchSize)
        {
            return await _context.OutboxMessages
                .AsNoTracking()
                .Where(message => message.FailedAt != null && message.ProcessedAt == null)
                .OrderBy(message => message.FailedAt)
                .Take(batchSize)
                .ToListAsync();
        }

        public async Task<bool> RequeueFailedAsync(Guid id, DateTime nextAttemptAt)
        {
            var rowsAffected = await _context.OutboxMessages
                .Where(message => message.Id == id && message.FailedAt != null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.FailedAt, (DateTime?)null)
                    .SetProperty(message => message.NextAttemptAt, nextAttemptAt)
                    .SetProperty(message => message.Error, (string?)null)
                    .SetProperty(message => message.RetryCount, 0));
            return rowsAffected == 1;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
