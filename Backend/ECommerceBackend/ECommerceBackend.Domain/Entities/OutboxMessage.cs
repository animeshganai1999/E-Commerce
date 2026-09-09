using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("OutboxMessages")]
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public Guid? AggregateId { get; set; }
        public string Type { get; set; } = default!;      // e.g. "OrderConfirmed"
        public string Payload { get; set; } = default!;   // JSON (e.g. { orderId })
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }        // null = not yet processed
        public DateTime? PublishedAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public int RetryCount { get; set; }
        public string? Error { get; set; }

        public void MarkPublished(DateTime publishedAt)
        {
            PublishedAt = publishedAt;
            ProcessedAt = publishedAt;
            LastAttemptAt = publishedAt;
            NextAttemptAt = null;
            Error = null;
        }

        public void RecordFailure(
            string error,
            DateTime attemptedAt,
            int maxAutomaticAttempts)
        {
            RetryCount += 1;
            LastAttemptAt = attemptedAt;
            Error = error.Length <= 2000 ? error : error[..2000];
            if (RetryCount >= maxAutomaticAttempts)
            {
                FailedAt = attemptedAt;
                NextAttemptAt = null;
                return;
            }

            var delaySeconds = Math.Min(300, Math.Pow(2, RetryCount));
            NextAttemptAt = attemptedAt.AddSeconds(delaySeconds);
        }

        public void Requeue(DateTime nextAttemptAt)
        {
            FailedAt = null;
            NextAttemptAt = nextAttemptAt;
            Error = null;
            RetryCount = 0;
        }
    }
}
