using StackExchange.Redis;
using System.Text.Json;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public class IdempotencyRepository : IIdempotencyRepository
    {
        private readonly IDatabase _db;
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public IdempotencyRepository(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task<IdempotencyClaim> ClaimAsync(
            string key,
            string requestHash,
            string leaseId,
            TimeSpan processingLease)
        {
            var processingRecord = new IdempotencyRecord(
                IdempotencyRecordState.Processing,
                requestHash,
                leaseId,
                null);
            var serializedRecord = Serialize(processingRecord);
            const string script = """
                local existing = redis.call('GET', KEYS[1])
                if existing then
                    return {1, existing}
                end

                redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
                return {0, ARGV[1]}
                """;

            var result = (RedisResult[]?)await _db.ScriptEvaluateAsync(
                script,
                [ScopedKey(key)],
                [serializedRecord, (long)processingLease.TotalMilliseconds])
                ?? throw new InvalidOperationException("Redis returned an invalid idempotency claim result.");

            if (result.Length != 2)
                throw new InvalidOperationException("Redis returned an incomplete idempotency claim result.");

            var status = (IdempotencyClaimStatus)(long)result[0];
            return new IdempotencyClaim(status, Deserialize((string?)result[1]));
        }

        public async Task<bool> CompleteAsync(
            string key,
            IdempotencyRecord processingRecord,
            IdempotencyResponse response,
            TimeSpan completedTtl)
        {
            var completedRecord = processingRecord with
            {
                State = IdempotencyRecordState.Completed,
                Response = response
            };

            return await CompareAndSetAsync(
                key,
                Serialize(processingRecord),
                Serialize(completedRecord),
                completedTtl);
        }

        public async Task<bool> ReleaseAsync(string key, IdempotencyRecord processingRecord)
        {
            const string script = """
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                end

                return 0
                """;

            var result = await _db.ScriptEvaluateAsync(
                script,
                [ScopedKey(key)],
                [Serialize(processingRecord)]);
            return (long)result == 1;
        }

        public async Task<bool> RenewAsync(
            string key,
            IdempotencyRecord processingRecord,
            TimeSpan processingLease)
        {
            const string script = """
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('PEXPIRE', KEYS[1], ARGV[2])
                end

                return 0
                """;

            var result = await _db.ScriptEvaluateAsync(
                script,
                [ScopedKey(key)],
                [Serialize(processingRecord), (long)processingLease.TotalMilliseconds]);
            return (long)result == 1;
        }

        private async Task<bool> CompareAndSetAsync(
            string key,
            string expected,
            string replacement,
            TimeSpan ttl)
        {
            const string script = """
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
                    return 1
                end

                return 0
                """;

            var result = await _db.ScriptEvaluateAsync(
                script,
                [ScopedKey(key)],
                [expected, replacement, (long)ttl.TotalMilliseconds]);
            return (long)result == 1;
        }

        private static string Serialize(IdempotencyRecord record) =>
            JsonSerializer.Serialize(record, SerializerOptions);

        private static IdempotencyRecord Deserialize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Redis returned an empty idempotency record.");

            return JsonSerializer.Deserialize<IdempotencyRecord>(value, SerializerOptions)
                ?? throw new InvalidOperationException("Redis returned an invalid idempotency record.");
        }

        private static string ScopedKey(string key) => $"idempotency:v2:{key}";
    }
}
