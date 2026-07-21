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

        // ---- RESERVE: atomic dual write ----
        // KEYS[1] = stock:{productId}
        // KEYS[2] = reservation:{orderId}:{productId}   (functional key)
        // KEYS[3] = reservations:index                  (ZSET)
        // ARGV[1] = qty, ARGV[2] = ttlSeconds, ARGV[3] = expiryUnix, ARGV[4] = member
        private const string ReserveScript = @"
            local stock = tonumber(redis.call('GET', KEYS[1]))
            if stock == nil then return -2 end
            if stock < tonumber(ARGV[1]) then return -1 end
            redis.call('DECRBY', KEYS[1], ARGV[1])
            redis.call('SET', KEYS[2], ARGV[1], 'EX', tonumber(ARGV[2]))
            redis.call('ZADD', KEYS[3], tonumber(ARGV[3]), ARGV[4])
            return 1";

        public async Task<ReserveResult> TryReserveAsync(Guid orderId, int productId, int quantity, TimeSpan ttl)
        {
            var expiryUnix = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
            var member = $"{orderId}:{productId}:{quantity}";

            var result = (long)await _db.ScriptEvaluateAsync(
                ReserveScript,
                new RedisKey[]
                {
                    $"stock:{productId}",
                    $"reservation:{orderId}:{productId}",
                    IndexKey
                },
                new RedisValue[]
                {
                    quantity,
                    (long)ttl.TotalSeconds,
                    expiryUnix,
                    member
                });

            return result switch
            {
                1 => ReserveResult.Success,
                -1 => ReserveResult.Insufficient,
                _ => ReserveResult.StockMissing   // -2
            };
        }

        // ---- CONFIRM: reservation becomes permanent, remove from expiry queue ----
        // KEYS[1] = reservation:{orderId}:{productId}
        // KEYS[2] = reservations:index
        // ARGV[1] = member
        private const string ConfirmScript = @"
            redis.call('DEL', KEYS[1])
            redis.call('ZREM', KEYS[2], ARGV[1])
            return 1";

        public async Task ConfirmAsync(Guid orderId, int productId, int quantity)
        {
            var member = $"{orderId}:{productId}:{quantity}";
            await _db.ScriptEvaluateAsync(
                ConfirmScript,
                new RedisKey[] { $"reservation:{orderId}:{productId}", IndexKey },
                new RedisValue[] { member });
        }

        // ---- RELEASE: return stock + cleanup (payment failed / cancelled) ----
        // KEYS[1] = stock:{productId}
        // KEYS[2] = reservation:{orderId}:{productId}
        // KEYS[3] = reservations:index
        // ARGV[1] = qty, ARGV[2] = member
        private const string ReleaseScript = @"
            redis.call('INCRBY', KEYS[1], tonumber(ARGV[1]))
            redis.call('DEL', KEYS[2])
            redis.call('ZREM', KEYS[3], ARGV[2])
            return 1";

        public async Task ReleaseAsync(Guid orderId, int productId, int quantity)
        {
            var member = $"{orderId}:{productId}:{quantity}";
            await _db.ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[]
                {
                    $"stock:{productId}",
                    $"reservation:{orderId}:{productId}",
                    IndexKey
                },
                new RedisValue[] { quantity, member });
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
                        $"stock:{productId}",
                        $"reservation:{orderId}:{productId}",
                        IndexKey
                    },
                    new RedisValue[] { qty, member });

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
                $"stock:{productId}", quantity,
                expiry: idleTtl,                 // null = no expiry; or e.g. 6h for cold eviction
                when: When.NotExists);           // don't clobber a live counter
        }

        public async Task WarmUpAsync(IEnumerable<(int Id, int Stock)> items)
        {
            // Batch for efficiency; force-set the authoritative SQL value for the sale.
            var batch = _db.CreateBatch();
            var tasks = new List<Task>();
            foreach (var (id, stock) in items)
                tasks.Add(batch.StringSetAsync($"stock:{id}", stock));  // no NX — refresh
            batch.Execute();
            await Task.WhenAll(tasks);
        }

        public async Task PreloadStockAsync(int productId, int quantity)
        {
            await _db.StringSetAsync($"stock:{productId}", quantity);
        }

        public async Task<long?> GetStockAsync(int productId)
        {
            var val = await _db.StringGetAsync($"stock:{productId}");
            return val.HasValue ? (long?)val : null;
        }

        public async Task SetStockAsync(int productId, int quantity)
        {
            await _db.StringSetAsync($"stock:{productId}", quantity);
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
                    var s = ((string)key!).Substring("stock:".Length);
                    if (int.TryParse(s, out int id))
                        ids.Add(id);
                }
            }
            return Task.FromResult(ids);
        }
    }
}