using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage message);
        Task<List<OutboxMessage>> GetReadyAsync(int batchSize, DateTime asOfUtc);
        Task MarkPublishedAsync(Guid id, DateTime publishedAt);
        Task RecordFailureAsync(Guid id, string error, DateTime attemptedAt);
        Task<List<OutboxMessage>> GetFailedAsync(int batchSize);
        Task<bool> RequeueFailedAsync(Guid id, DateTime nextAttemptAt);
        Task SaveChangesAsync();
    }
}
