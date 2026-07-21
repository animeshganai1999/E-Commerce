using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage message);
        Task<List<OutboxMessage>> GetUnprocessedAsync(int batchSize);
        Task MarkProcessedAsync(Guid id);
        Task MarkFailedAsync(Guid id, string error);
        Task SaveChangesAsync();
    }
}
