namespace ECommerceBackend.Infrastructure.Repositories
{
    public enum IdempotencyRecordState
    {
        Processing,
        Completed
    }

    public sealed record IdempotencyResponse(
        int StatusCode,
        string? ContentType,
        byte[] Body,
        Dictionary<string, string[]> Headers);

    public sealed record IdempotencyRecord(
        IdempotencyRecordState State,
        string RequestHash,
        string LeaseId,
        IdempotencyResponse? Response);

    public enum IdempotencyClaimStatus
    {
        Acquired,
        Existing
    }

    public sealed record IdempotencyClaim(
        IdempotencyClaimStatus Status,
        IdempotencyRecord? Record);

    public interface IIdempotencyRepository
    {
        Task<IdempotencyClaim> ClaimAsync(
            string key,
            string requestHash,
            string leaseId,
            TimeSpan processingLease);

        Task<bool> CompleteAsync(
            string key,
            IdempotencyRecord processingRecord,
            IdempotencyResponse response,
            TimeSpan completedTtl);

        Task<bool> RenewAsync(
            string key,
            IdempotencyRecord processingRecord,
            TimeSpan processingLease);

        Task<bool> ReleaseAsync(string key, IdempotencyRecord processingRecord);
    }
}