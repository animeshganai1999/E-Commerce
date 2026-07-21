using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("OutboxMessages")]
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = default!;      // e.g. "OrderConfirmed"
        public string Payload { get; set; } = default!;   // JSON (e.g. { orderId })
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }        // null = not yet processed
        public int RetryCount { get; set; }
        public string? Error { get; set; }
    }
}
