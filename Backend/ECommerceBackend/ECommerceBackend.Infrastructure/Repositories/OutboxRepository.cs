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

        public async Task<List<OutboxMessage>> GetUnprocessedAsync(int batchSize)
        {
            const int maxRetries = 5;
            return await _context.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount < maxRetries)
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync();
        }

        public async Task MarkProcessedAsync(Guid id)
        {
            var msg = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (msg == null) return;
            msg.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task MarkFailedAsync(Guid id, string error)
        {
            var msg = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (msg == null) return;
            msg.RetryCount += 1;
            msg.Error = error;
            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
