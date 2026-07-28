using StackExchange.Redis;

namespace ECommerceBackend.Infrastructure.Repositories
{
    public enum ReserveResult { Success, Insufficient, StockMissing }
    public class StockReservationRepository : IStockReservationRepository
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _redis;
        private const string IndexKey = "reservations:index"; // the ZSET tracker

        public StockReservationRepository(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = redis.GetDatabase();
        }

        // Hash-tagged keys so a product's stock counter and its reservation land in the SAME
        // Redis Cluster slot (Azure Managed Redis runs clustered). Redis hashes only on the
        // "{productId}" tag, which makes multi-key Lua scripts over these two keys legal.
        // The shared "reservations:index" ZSET can't co-locate with every product, so its
        // ZADD/ZREM are done as separate single-key calls (cluster-safe) outside the scripts.
        private static RedisKey StockKey(int productId) => (RedisKey)$"stock:{{{productId}}}";
        private static RedisKey ReservationKey(Guid orderId, int productId)
            => (RedisKey)$"reservation:{{{productId}}}:{orderId}";

        // ---- RESERVE: atomic stock check + decrement + reservation (same slot) ----
        // KEYS[1] = stock:{productId}
        // KEYS[2] = reservation:{productId}:{orderId}   (functional key)
        // ARGV[1] = qty, ARGV[2] = ttlSeconds
        private const string ReserveScript = @"
            local stock = tonumber(redis.call('GET', KEYS[1]))
            if stock == nil then return -2 end
            if stock < tonumber(ARGV[1]) then return -1 end
            redis.call('DECRBY', KEYS[1], ARGV[1])
            redis.call('SET', KEYS[2], ARGV[1], 'EX', tonumber(ARGV[2]))
            return 1";

        public async Task<ReserveResult> TryReserveAsync(Guid orderId, int productId, int quantity, TimeSpan ttl)
        {
            var expiryUnix = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
            var member = $"{orderId}:{productId}:{quantity}";

            var result = (long)await _db.ScriptEvaluateAsync(
                ReserveScript,
                new RedisKey[]
                {
                    StockKey(productId),
                    ReservationKey(orderId, productId)
                },
                new RedisValue[]
                {
                    quantity,
                    (long)ttl.TotalSeconds
                });

            // Track expiry in the global ZSET (single-key op -> cluster-safe) only on success.
            if (result == 1)
                await _db.SortedSetAddAsync(IndexKey, member, expiryUnix);

            return result switch
            {
                1 => ReserveResult.Success,
                -1 => ReserveResult.Insufficient,
                _ => ReserveResult.StockMissing   // -2
            };
        }

        // ---- CONFIRM: reservation becomes permanent, remove from expiry queue ----
        public async Task ConfirmAsync(Guid orderId, int productId, int quantity)
        {
            var member = $"{orderId}:{productId}:{quantity}";
            // Single-key ops (different slots on cluster) -> issue separately.
            await _db.KeyDeleteAsync(ReservationKey(orderId, productId));
            await _db.SortedSetRemoveAsync(IndexKey, member);
        }

        // ---- RELEASE: return stock + cleanup (payment failed / cancelled) ----
        // KEYS[1] = stock:{productId}
        // KEYS[2] = reservation:{productId}:{orderId}
        // ARGV[1] = qty
        private const string ReleaseScript = @"
            redis.call('INCRBY', KEYS[1], tonumber(ARGV[1]))
            redis.call('DEL', KEYS[2])
            return 1";

        public async Task ReleaseAsync(Guid orderId, int productId, int quantity)
        {
            var member = $"{orderId}:{productId}:{quantity}";
            await _db.ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[]
                {
                    StockKey(productId),
                    ReservationKey(orderId, productId)
                },
                new RedisValue[] { quantity });

            // Remove from the global ZSET separately (cluster-safe single-key op).
            await _db.SortedSetRemoveAsync(IndexKey, member);
        }

        // ===== RECLAIM (no lock — sweeper service coordinates) =====
        public async Task<int> ReclaimExpiredAsync()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expired = await _db.SortedSetRangeByScoreAsync(IndexKey, 0, now);

            int reclaimed = 0;
            foreach (var member in expired)
            {
                var parts = ((string)member!).Split(':');
                if (parts.Length != 3)
                {
                    await _db.SortedSetRemoveAsync(IndexKey, member);
                    continue;
                }

                var orderId = parts[0];
                var productId = parts[1];
                var qty = int.Parse(parts[2]);

                await _db.ScriptEvaluateAsync(
                    ReleaseScript,
                    new RedisKey[]
                    {
                        StockKey(int.Parse(productId)),
                        ReservationKey(Guid.Parse(orderId), int.Parse(productId))
                    },
                    new RedisValue[] { qty });

                await _db.SortedSetRemoveAsync(IndexKey, member);

                reclaimed++;
            }
            return reclaimed;
        }

        // ===== DISTRIBUTED LOCK (generic, reusable) =====

        // Safe unlock: delete only if the stored token is still ours.
        private const string UnlockScript = @"
            if redis.call('GET', KEYS[1]) == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            else
                return 0
            end";

        // Generic distributed lock — reusable for any background job (sweeper, outbox, ...).
        public async Task<string?> AcquireLockAsync(string lockKey, TimeSpan ttl)
        {
            var token = Guid.NewGuid().ToString();
            bool acquired = await _db.StringSetAsync(lockKey, token, expiry: ttl, when: When.NotExists);
            return acquired ? token : null;
        }

        public async Task ReleaseLockAsync(string lockKey, string token)
        {
            await _db.ScriptEvaluateAsync(
                UnlockScript,
                new RedisKey[] { lockKey },
                new RedisValue[] { token });
        }

        public async Task PopulateStockIfAbsentAsync(int productId, int quantity, TimeSpan? idleTtl = null)
        {
            await _db.StringSetAsync(
                StockKey(productId), quantity,
                expiry: idleTtl,                 // null = no expiry; or e.g. 6h for cold eviction
                when: When.NotExists);           // don't clobber a live counter
        }

        public async Task WarmUpAsync(IEnumerable<(int Id, int Stock)> items)
        {
            // Batch for efficiency; force-set the authoritative SQL value for the sale.
            var batch = _db.CreateBatch();
            var tasks = new List<Task>();
            foreach (var (id, stock) in items)
                tasks.Add(batch.StringSetAsync(StockKey(id), stock));  // no NX — refresh
            batch.Execute();
            await Task.WhenAll(tasks);
        }

        public async Task PreloadStockAsync(int productId, int quantity)
        {
            await _db.StringSetAsync(StockKey(productId), quantity);
        }

        public async Task<long?> GetStockAsync(int productId)
        {
            var val = await _db.StringGetAsync(StockKey(productId));
            return val.HasValue ? (long?)val : null;
        }

        public async Task SetStockAsync(int productId, int quantity)
        {
            await _db.StringSetAsync(StockKey(productId), quantity);
        }

        // Enumerate the "hot" product ids currently held in Redis via SCAN (non-blocking).
        // Uses IServer.Keys, which iterates with SCAN under the hood (never KEYS).
        public Task<List<int>> GetTrackedProductIdsAsync()
        {
            var ids = new List<int>();
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;

                foreach (var key in server.Keys(database: _db.Database, pattern: "stock:*", pageSize: 500))
                {
                    // Keys are hash-tagged as "stock:{id}" -> strip the braces to parse the id.
                    var s = ((string)key!).Substring("stock:".Length).Trim('{', '}');
                    if (int.TryParse(s, out int id))
                        ids.Add(id);
                }
            }
            return Task.FromResult(ids);
        }

        // DEV/TEST ONLY: flush all master nodes so a load test starts from a clean slate.
        public async Task FlushAllAsync()
        {
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;
                await server.FlushDatabaseAsync(_db.Database);
            }
        }
    }
}