# Notes: Redis Inventory, Outbox Pattern & SQL Sync

> How the scalable checkout system works: Redis for the hot path (reservations),
> the Outbox pattern for crash-safe capture, **Azure Service Bus** for reliable post-payment
> fulfillment, and how everything stays in sync with SQL Server (the durable source of truth).

---

## 1. Why Redis for Stock?

A hot product (flash sale, "10 left") gets **thousands of concurrent buyers** hitting the
**same stock row**. In SQL, that single row becomes a lock bottleneck — throughput collapses.

**Redis solves this** because:
- It is **single-threaded per key** -> operations on `stock:{id}` are naturally serialized.
- **Lua scripts** run atomically inside Redis (check + decrement = one uninterruptible step).
- Handles ~100k ops/sec on one node — far beyond a SQL row lock.

**Where Redis actually helps:** at the **RESERVE** step — the moment of extreme concurrency.
By confirm/settle, only the few "winners" remain (low volume) -> SQL handles those fine.

```
RESERVE:  10,000 requests -> Redis   (STORM - Redis essential)
                              | only ~100 winners pass
CONFIRM:     100 requests -> SQL     (TRICKLE - SQL is fine)
SETTLE:      100 writes   -> SQL     (TRICKLE)
```

> Rule of thumb: Redis = **fast working counter** (hot path). SQL = **durable source of truth**.
> Redis does NOT help "add to cart" for **contention** (cart is per-user, no oversell risk), but a
> per-user Redis **read-cache** still offloads the frequent cart reads from SQL (see Section 16).

---

## 2. The Redis Data Structures

| Key | Type | Purpose |
|-----|------|---------|
| `stock:{productId}` | STRING (number) | Available units (the live counter) |
| `reservation:{orderId}:{productId}` | STRING + TTL | Functional record of one reservation |
| `reservations:index` | SORTED SET (ZSET) | Expiry tracker — the sweeper reads this |
| `idempotency:{key}` | STRING + TTL | Duplicate-request guard for checkout |
| `lock:reservation-sweeper` | STRING (NX+EX) | Distributed lock for the sweeper |

ZSET tracker: member = `"orderId:productId:qty"`, score = expiry Unix timestamp.

---

## 3. The Dual-Write Pattern (reservation)

On every reservation we write **both**:
1. The **functional STRING key** (with TTL) — the app reads/confirms this.
2. A **ZSET tracker entry** — a durable, ordered "to-expire" queue.

Why two writes? A TTL only **deletes the reservation record** — it does **NOT** return stock
(`stock:{id}` is a separate key). The ZSET lets a **sweeper** reliably find expired
reservations and return stock — deterministically, even after a restart.

---

## 4. Redis Operations (all Lua-atomic)

| Op | Redis effect |
|----|--------------|
| **RESERVE** (`TryReserveAsync`) | `GET stock` -> if `< qty` reject; else `DECRBY` + `SET reservation EX` + `ZADD index` |
| **CONFIRM** (`ConfirmAsync`) | `DEL reservation` + `ZREM index` (permanent — sweeper won't reclaim) |
| **RELEASE** (`ReleaseAsync`) | `INCRBY stock` + `DEL reservation` + `ZREM index` |
| **RECLAIM** (sweeper) | `ZRANGEBYSCORE 0 now` -> for each: release (INCRBY + cleanup) |

Reserve return codes: `1` success, `-1` insufficient, `-2` key missing (-> lazy load).

---

## 5. Idempotency (three layers)

The system is safe against duplicate processing at **three** independent levels — important
because a queue (and network retries) deliver **at-least-once**.

### Layer 1 — Request-level (`[Idempotent]` filter)
Stops duplicate HTTP requests (double-click / network retry) from creating two orders.
```
[Idempotent] filter (before controller):
   TryClaimAsync(key)  -> SET idempotency:{key} "in_progress" NX EX
      - new         -> process -> cache the response under the key
      - in_progress -> 409 Conflict (duplicate mid-flight)
      - completed   -> replay the cached response (no re-processing)
```
- Backed by Redis (`IdempotencyRepository`), key = client's `Idempotency-Key` header.
- Applied to `POST /checkout/begin`.

### Layer 2 — Confirm-level (`Order.Status` guard)
Stops a second confirm from re-processing an order.
```csharp
if (order.Status != OrderStatus.Pending) return; // already confirmed
```

### Layer 3 — Settlement-level (`Order.StockSettledAt` guard)
Stops the outbox relay from double-deducting stock if a message is reprocessed
(crash before MarkProcessed, the same message picked up twice, or a Service Bus publish retry).
```csharp
if (order.StockSettledAt != null) return; // already settled
```

| Layer | Guard | Protects against |
|-------|-------|------------------|
| Request | `[Idempotent]` filter (Redis SET NX) | Double-click / retry -> duplicate orders |
| Confirm | `Order.Status != Pending` | Re-confirming the same order |
| Settlement | `Order.StockSettledAt != null` | Outbox reprocessing -> double stock deduction |

---

## 6. The Outbox Pattern (crash-safe settlement)

### The dual-write problem
Confirm must update **Redis** (finalize reservation) AND **SQL** (`Products.StockQuantity`).
Doing both inline is **not atomic** — a crash between them causes **drift** (Redis says sold,
SQL not deducted).

### The fix
Confirm writes **only ONE atomic SQL transaction**:
```
SQL transaction (atomic):
   UPDATE Order SET Status = Confirmed
   INSERT OutboxMessage("OrderConfirmed", { orderId })
commit
```
The **intent to settle** is saved atomically with the status. A background **outbox processor**
then performs the actual Redis confirm + SQL deduction — reliably, with retries.

### The outbox relay (`OutboxRelayService`, every 5s)
The relay **settles stock inline** (fast + critical) and then **publishes** the slow fulfillment
work to **Azure Service Bus** (see Section 19). It no longer does the invoice/email itself.
```
GetUnprocessedAsync() -> for each "OrderConfirmed":
    if order.StockSettledAt != null -> skip (idempotent)
    settle:   Redis ConfirmAsync + SQL TryDeductStockAsync (per item)
    mark:     MarkStockSettledAsync(order)  -> StockSettledAt = now  (BEFORE publish)
    publish:  Service Bus "order-fulfillment" { orderId }
    MarkProcessedAsync(message)  -> ProcessedAt = now
    (on failure -> MarkFailedAsync -> retried next cycle)
```
The **FulfillmentWorker** (Service Bus consumer) then does the slow work: invoice PDF + email +
save invoice record — with independent retries + automatic dead-lettering (Section 19).

### Why it's crash-safe
| Failure point | Result |
|---------------|--------|
| Crash after confirm, before settle | Outbox row persists -> relay retries |
| Relay crashes mid-settle | Message still unprocessed -> retried |
| Message processed twice | `StockSettledAt` guard -> no double deduction |
| Settle ok, publish fails | Outbox retries; `StockSettledAt` guard skips re-settle, just re-publishes |
| Fulfillment (invoice/email) fails | Service Bus redelivers; auto dead-letters after MaxDeliveryCount |

> **Two-stage decoupling now implemented:** Outbox = atomic capture (SQL); the relay settles
> stock and hands slow fulfillment to **Azure Service Bus** = reliable transport with independent
> retries + DLQ. See Section 19.

---

## 7. Full Checkout Flow (two-step: reserve -> pay -> confirm)

```
STEP 1 - POST /checkout/begin (Idempotency-Key: abc)
   |
   |-[Idempotency] claim abc (Redis SET NX) -- duplicate? -> replay cached response
   |
   |-[Reserve]  Redis: DECRBY stock + SET reservation + ZADD index   (Redis STORM handled here)
   |            SQL:   INSERT Order(Pending) + OrderItems + billing snapshot
   |            out of stock? -> release -> 409
   |
   |-> return { orderId }   (fast — stock held by TTL while user pays)

STEP 2 - POST /payment/pay { orderId, success }   (dummy payment for now)
   |
   |-- success -> ConfirmStockAsync
   |               SQL txn: Order=Confirmed + OutboxMessage   (ATOMIC - dual-write fixed)
   |               -> 200 fast
   |
   |-- failure -> ReleaseStockAsync (Redis INCRBY + Order=Cancelled) -> 402

BACKGROUND - OutboxRelayService (every 5s):
   settle stock:  Redis confirm (remove reservation) + SQL Products.StockQuantity -= qty
   mark:          StockSettledAt + ProcessedAt   (idempotent)
   publish:       Service Bus queue "order-fulfillment" { orderId }
         |
         v
BACKGROUND - FulfillmentWorker (Service Bus consumer):
   fulfill:       generate invoice PDF (from the persisted Order) + email + save invoice record
   (auto retry + dead-letter via Service Bus)
```

> The invoice PDF + email now run **in the background worker**, built from the **persisted
> Order** (not the cart) — so checkout returns fast and Redis's reserve fully scales.
> `BeginCheckoutAsync` captures a **billing snapshot** on the order so the worker doesn't
> need the cart.

---

## 8. Stock Values at Each Stage

| Stage | Redis `stock` | SQL `StockQuantity` | Meaning |
|-------|:--:|:--:|---------|
| Before checkout | 100 | 100 | in sync |
| After reserve | 92 | 100 | 8 held (not sold) |
| After confirm | 92 | 100 | confirmed, settlement pending |
| After outbox settle | 92 | 92 | fully in sync — sold |
| If abandoned/expired | 100 (swept back) | 100 | released |

---

## 9. SQL Synchronization Points

| Direction | When | Mechanism |
|-----------|------|-----------|
| SQL -> Redis | Lazy load on cache miss (`-2`) | `GetStockFromSqlAsync` -> `PopulateStockIfAbsentAsync` (NX) |
| SQL -> Redis | Flash-sale warm-up | `WarmUpAsync` (bulk force-set) |
| Redis -> SQL | Order settlement | **Outbox relay** -> `TryDeductStockAsync` (then publishes fulfillment to Service Bus) |
| SQL <-> Redis | Periodic reconciliation | (recommended) recompute `stock` from SQL - open reservations |

Consistency is **eventual** — Redis is the fast hot counter, SQL is updated asynchronously via
the outbox. Correctness on the hot path (reserve) is **immediate**.

---

## 10. Hybrid Loading (1M+ catalog)

- **Lazy load (default):** load a product's stock into Redis only on first request (`-2` miss ->
  SQL -> populate with `When.NotExists` -> retry). Redis holds only **hot** items.
- **Pre-warm (sales):** bulk-load specific sale items into Redis **before** the event
  (`WarmUpProductsAsync`) to avoid a thundering-herd of cache misses.
- **Eviction:** Redis `maxmemory-policy allkeys-lru` drops cold items automatically.
- **Sharding (mega-hot items):** split one item's stock across N keys (`stock:{id}:0..9`).

---

## 11. Multiple Instances (Horizontal Scaling)

All instances share the **same Redis**, so counters/reservations are automatically consistent.
But **per-process background jobs run on every instance** and need coordination:

| Component | Issue | Fix |
|-----------|-------|-----|
| Reserve/Confirm/Release (Lua) | none | Atomic on shared Redis |
| **Sweeper** | every instance sweeps | Distributed lock `lock:reservation-sweeper` |
| **Outbox processor** | every instance polls | Distributed lock `lock:outbox-processor` |
| **Reconciliation** | every instance reconciles | Distributed lock `lock:stock-reconciliation` |
| **Preload / warm-up** | every instance overwrites | `When.NotExists` guard or one-time seeding |

All three background jobs use the **same generic distributed lock** on the reservation
repository: `AcquireLockAsync(key, ttl)` / `ReleaseLockAsync(key, token)`.

Distributed lock: `SET lock NX EX 25` to claim; **Lua compare-and-delete** to release only your
own token. Lock TTL < poll interval, but > worst-case job duration.

> Principle: **shared state in Redis/SQL = safe across instances**;
> **per-process background jobs = need a distributed lock or dedicated worker.**

---

## 12. Background Hosted Services

Three `IHostedService` background workers keep the system consistent. Each runs on **every**
instance but is guarded by a **distributed lock** so only one instance acts per cycle.
All use `CreateScope()` per iteration (singleton service -> scoped repositories).

| Service | Interval | Lock key | Job |
|---------|----------|----------|-----|
| `ReservationSweeperService` | 30s | `lock:reservation-sweeper` | Reclaim expired reservations (ZRANGEBYSCORE -> INCRBY stock) |
| `OutboxRelayService` | 5s | `lock:outbox-processor` | Settle stock (Redis + SQL) then **publish** fulfillment to Service Bus |
| `FulfillmentWorker` | event-driven | (Service Bus lease) | Consume `order-fulfillment` queue: invoice PDF + email + persist |
| `StockReconciliationService` | 5min | `lock:stock-reconciliation` | Self-heal Redis<->SQL drift + fail expired Pending orders |

### `ReservationSweeperService`
- Reads `reservations:index` (ZSET) for members with score <= now (expired).
- For each: `INCRBY stock` + `DEL reservation` + `ZREM index` (Lua-atomic).
- Returns stock abandoned mid-checkout.

### `OutboxRelayService`
- Reads unprocessed `OutboxMessages` (skips those with `RetryCount >= 5` = dead-lettered).
- For each `OrderConfirmed`: guard on `StockSettledAt`, then Redis confirm + SQL
  `TryDeductStockAsync`, `MarkStockSettledAsync`, then **publish** an `OrderFulfillment` message
  to Azure Service Bus, then `MarkProcessedAsync`; on error `MarkFailedAsync` (retried, capped at 5).
- Settling **before** publishing + the `StockSettledAt` guard means a publish retry never
  re-deducts stock (Section 19).

### `FulfillmentWorker` (Service Bus consumer)
- A `ServiceBusProcessor` on the `order-fulfillment` queue (event-driven, not polled).
- For each message: generate invoice PDF (from the persisted Order) + email + save invoice record,
  then `CompleteMessageAsync`. On failure `AbandonMessageAsync` -> Service Bus redelivers, and
  auto **dead-letters** after `MaxDeliveryCount` (10). See Section 19.

### `StockReconciliationService`
- Enforces invariant: `Redis stock = SQL StockQuantity - SUM(open Pending reservations)`.
- **Redis-driven (scales to 1M+ catalog):** only items *in* Redis can drift, so it enumerates
  the **hot set** via `SCAN` (`GetTrackedProductIdsAsync` -> `IServer.Keys(pattern:"stock:*")`),
  then fetches SQL stock + pending reservations **only for those ids** (`GetStockForManyAsync`,
  `GetPendingReservedQuantitiesForAsync`). Corrects any drifted **existing** key; cold/evicted
  items are left to lazy-load.
- Marks expired `Pending` orders as `Failed` (removes them from the reservation count).
- Catches drift that other mechanisms miss (Redis restart/eviction, dead-lettered settlements,
  manual SQL edits, orphaned reservations).

> **Why SCAN, not KEYS:** `KEYS pattern` returns everything in one shot and **blocks Redis**;
> `SCAN` (used by `IServer.Keys`, `pageSize: 500`) iterates in **non-blocking batches**.
> Product keys are found purely by the **`stock:` prefix convention** (see Section 13) — the
> prefix acts as a logical "namespace", so `stock:*` matches only product-stock keys and ignores
> `reservation:*`, `idempotency:*`, `lock:*`. The `{id}` is parsed from the key name.

### Resiliency built in
- EF Core `EnableRetryOnFailure(5, 10s)` -> transient SQL faults auto-retry.
- Outbox max-retry (5) -> broken messages stop looping (de-facto dead-letter via `Error` column).
- Health checks: `GET /health` (SQL + Redis).

---

## 13. Redis Keys Reference

| Key pattern | Type | Written by | Purpose / TTL |
|-------------|------|-----------|---------------|
| `stock:{productId}` | STRING (int) | reserve/release/settle/reconcile | Live available counter |
| `reservation:{orderId}:{productId}` | STRING | reserve | One hold; TTL = reservation window |
| `reservations:index` | ZSET | reserve | Expiry tracker (member=`orderId:productId:qty`, score=expiry) |
| `idempotency:{key}` | STRING | `[Idempotent]` filter | Duplicate-request guard; TTL ~24h |
| `lock:reservation-sweeper` | STRING (NX+EX) | sweeper | Distributed lock |
| `lock:outbox-processor` | STRING (NX+EX) | outbox processor | Distributed lock |
| `lock:stock-reconciliation` | STRING (NX+EX) | reconciliation | Distributed lock |
| `cache:products:page:{category\|all}:{page}:{pageSize}` | STRING (JSON) | product read-cache | One catalog page; TTL 5m (Section 15) |
| `cache:products:cursor:{category\|all}:{afterId\|start}:{pageSize}` | STRING (JSON) | product read-cache | One keyset "Load more" batch; TTL 5m (Section 17) |
| `cache:products:{productId}` | STRING (JSON) | product read-cache | Single product detail; TTL 5m (Section 15) |
| `cache:cart:{userId}` | STRING (JSON) | cart read-cache | One user's cart; TTL 10m, invalidated on write (Section 16) |

**Naming convention = namespacing.** Redis is a flat key-value store; the `type:id` prefix
(`stock:`, `reservation:`, `idempotency:`, `lock:`) acts as a logical "table". This is what lets
`SCAN pattern "stock:*"` enumerate only product-stock keys. **Always `SCAN`, never `KEYS`**
(`KEYS` blocks the whole server). `IServer.Keys(pattern, pageSize)` uses `SCAN` internally.

---

## 14. Failure Scenarios

| Scenario | What happens |
|----------|--------------|
| App crash mid-reserve | Lua atomicity -> all 3 writes or none (no leak) |
| User abandons checkout | TTL passes -> sweeper `INCRBY` returns stock |
| Payment/email fails | `ReleaseAsync` returns stock immediately (payment); email failure -> Service Bus retries then DLQ |
| Crash after confirm, before settle | Outbox row persists -> relay retries (no drift) |
| Outbox processed twice | `StockSettledAt` guard -> no double deduction |
| Settle ok but Service Bus publish fails | Outbox retries; `StockSettledAt` skips re-settle, just re-publishes |
| Fulfillment (invoice/email) fails repeatedly | Service Bus dead-letters after MaxDeliveryCount (10) |
| Duplicate checkout request | Idempotency key -> replay cached response |
| Redis restarts (data lost) | Lazy load re-populates from SQL; reconciliation re-aligns held counts |
| Redis key evicted (LRU) while reserved | Reconciliation recomputes `SQL - open reservations` |
| Outbox hits max retries (5) | Skipped (dead-letter via `Error`); reconciliation flags drift |
| Expired Pending order never settled | Reconciliation marks it `Failed` |
| Transient SQL fault | EF `EnableRetryOnFailure` auto-retries |

---

## 15. Product Catalog Read-Cache (cache-aside)

Separate from the **stock hot path** above, the read-heavy catalog (`GET /api/products`,
`GET /api/products/{id}`) is served through a Redis **cache-aside** layer (`ProductCache`).
It reuses the **same `IConnectionMultiplexer`** as the reservation system — one Redis programming
model across the codebase (no `IDistributedCache`).

### Why a read-cache
The catalog is **read a lot, changes rarely**. Caching pages/products spares SQL the repeated
read load. **Live stock correctness is NOT affected** — oversell is still guarded at RESERVE by
the `stock:{id}` counters + SQL. The cache holds mostly-static fields (title, price, description,
image) with a **short 5-min TTL**, so any staleness self-heals quickly.

### Pagination (scales to 1M+ products)
`GET /api/products` is **paged + optionally filtered by category** — never "fetch all".
The repository does `WHERE Category=@c` + `OrderBy(Id)` + `Skip/Take` + `Count` (all
`AsNoTracking`), backed by an **index on `Product.Category`**. Each **page** is cached as its own
small key, so no single giant key ever holds the whole catalog.

> **Two pagination styles are available** — offset (numbered pages) and keyset ("Load more").
> See **Section 17** for the keyset/cursor endpoint and why it scales better for deep scrolling.


### Cache-aside flow
```
GET page:
   hit  -> return cached page (no SQL)
   miss -> SQL page query -> SetPage (TTL 5m) -> return

GET by id:
   hit  -> return cached product
   miss -> SQL -> SetById (TTL 5m) -> return
```

### Category namespacing + case-insensitivity
Category is normalized to **lower-case** (`Trim().ToLowerInvariant()`) so `Electronics` and
`electronics` collapse to the **same** cache key and DB filter (SQL Server's default collation is
case-insensitive, so the `Category` index is still used). Each category — plus the unfiltered
`all` listing — gets its **own** set of page keys:

```
?page=1&pageSize=12                     -> cache:products:page:all:1:12
?page=1&pageSize=12&category=electronics -> cache:products:page:electronics:1:12
```

### No page-index / registry (deliberate)
Page keys **self-expire via TTL** — there is intentionally **no** set tracking page keys.
An earlier design kept a `page-index` SET for bulk purge, but `WarmUpProductsAsync` (the only
invalidator) is a **rare pre-sale action**, so that set would grow unbounded (leak) for little
benefit. `InvalidateAsync` now only clears the affected **per-id** keys; pages just expire.

### Cache keys
| Key pattern | Type | Written by | Purpose / TTL |
|-------------|------|-----------|---------------|
| `cache:products:page:{category|all}:{page}:{pageSize}` | STRING (JSON) | `GetProductsAsync` (miss) | One catalog page (items + total count); TTL 5m |
| `cache:products:{productId}` | STRING (JSON) | `GetProductByIdAsync` (miss) | Single product detail; TTL 5m |

> These are **read-cache** keys (prefix `cache:products:`), fully separate from the authoritative
> `stock:{id}` counters. Stale catalog data is bounded by the 5-min TTL; stock is always correct
> at reserve time regardless of this cache.

---

## 16. Cart Read-Cache (cache-aside)

Like the catalog cache (Section 15), a user's **cart** is served through a Redis **cache-aside**
layer (`CartCache`). It reuses the **same `IConnectionMultiplexer`** as the reservation system and
the product cache — one Redis programming model across the codebase (no `IDistributedCache`).

### Why a cart read-cache
The cart is **read on every page load** (navbar count, cart page, checkout) but changes only when
the user edits it. There is **no contention** here — a cart is **per-user**, so unlike stock it has
no oversell risk. The value is purely **offloading the repeated read** from SQL. Correctness is
maintained by **explicit invalidation on every write**, so the cache never serves a stale cart; a
short **10-min TTL** is only a safety net.

### Per-user key
Each user's cart is one Redis STRING keyed by user id — no giant shared key:
```
cache:cart:{userId}   ->   JSON( List<CartItem> )
```

### Cache-aside flow (`CartService`)
```
GET cart (GetCartByUserIdAsync):
   hit  -> return cached cart          (no SQL)
   miss -> SQL (GetCartByUserIdAsync) -> SetByUserAsync (TTL 10m) -> return

WRITE cart (ApplyCartDiffAsync):
   apply add/update/remove to SQL -> SaveChanges
   InvalidateAsync(userId)   -> DEL cache:cart:{userId}   (next read repopulates)
```

### Invalidate-on-write (not update-on-write)
`ApplyCartDiffAsync` **deletes** the key after persisting, rather than rewriting it. This keeps the
cache correct with minimal logic — the next read lazily repopulates from the authoritative SQL
state. It avoids any risk of the cache and SQL diverging after a partial diff (add + update +
remove in one call).

### Cache key
| Key pattern | Type | Written by | Purpose / TTL |
|-------------|------|-----------|---------------|
| `cache:cart:{userId}` | STRING (JSON) | `GetCartByUserIdAsync` (miss) | One user's cart items; TTL 10m, invalidated on every write |

> This is a **read-cache** key (prefix `cache:cart:`), separate from both the `stock:{id}` hot path
> and the `cache:products:*` catalog cache. Because it is invalidated on every write, staleness is
> effectively zero; the TTL is only a self-heal net for a missed invalidation.

---

## 17. Keyset (Cursor) Pagination — "Load More"

The catalog exposes **two** pagination styles. Both are cache-aside over the same Redis
(`IConnectionMultiplexer`) and both filter by category the same way; they differ only in **how the
client walks the list**.

| Endpoint | Style | Client control | Best for |
|----------|-------|----------------|----------|
| `GET /api/products` | **Offset** (`Skip/Take`) | page number | numbered page buttons, "jump to page N" |
| `GET /api/products/feed` | **Keyset / cursor** (`WHERE Id > cursor`) | opaque cursor | **"Load more" / infinite scroll** |

The frontend uses a **"Load more"** experience, so `feed` is the preferred endpoint.

### Why keyset over offset for deep lists
`OFFSET N` is **O(N + pageSize)** — the database must generate and discard the first `N` rows
before returning the page. The deeper you scroll, the slower it gets:

```
Batch 1     OFFSET 0       -> read 20 rows        fast
Batch 100   OFFSET 1,980   -> read 2,000 rows     slower
Batch 5000  OFFSET 99,980  -> read 100,000 rows   slow
```

Keyset instead **seeks** straight to the cursor position via the index — **O(pageSize)** at any
depth:

```
Batch 5000  WHERE Id > 99,980 ORDER BY Id  -> read ~21 rows   still fast
```

### The query (`ProductRepository.GetProductsByCursorAsync`)
```csharp
IQueryable<Product> query = _context.Products.AsNoTracking();
if (!string.IsNullOrWhiteSpace(category))
    query = query.Where(p => p.Category == category);
if (afterId.HasValue)
    query = query.Where(p => p.Id > afterId.Value);   // the seek

var rows = await query
    .OrderBy(p => p.Id)
    .Take(pageSize + 1)          // fetch ONE extra as a "there's more?" probe
    .ToListAsync();
```
Generated SQL (batch after cursor 20):
```sql
SELECT TOP (21) *
FROM Products
WHERE Category = @category AND Id > 20
ORDER BY Id;
```

### The `pageSize + 1` probe
Fetching one extra row is how the API knows whether to show a "Load more" button — with **no**
extra `COUNT(*)` query (offset pagination pays that count on every request):
```csharp
int? nextCursor = null;
if (rows.Count > pageSize)       // got the probe row back -> more exist
{
    rows.RemoveAt(pageSize);     // drop the probe
    nextCursor = rows[^1].Id;    // cursor for the next call = last item's Id
}
// fewer than pageSize+1 rows -> end of list -> nextCursor stays null
```

### Response shape (`CursorResult<T>`)
```jsonc
// GET /api/products/feed?afterId=20&pageSize=20
{
  "items":      [ /* products 21..40 */ ],
  "nextCursor": 40,     // pass as ?afterId=40 next time
  "hasMore":    true,   // false + nextCursor:null => hide "Load more"
  "pageSize":   20
}
```
First call omits `afterId` (starts at the beginning). Each subsequent "Load more" passes the
previous `nextCursor` as `afterId`.

### Cache-aside (cursor-keyed)
Same pattern as the offset cache, but keyed by the **cursor** instead of a page number:
```
GET feed:
   hit  -> return cached batch (no SQL)
   miss -> keyset SQL query -> SetCursor (TTL 5m) -> return
```
Key format (`start` for the first batch):
```
cache:products:cursor:{category|all}:{afterId|start}:{pageSize}
```
Examples:
```
?pageSize=20                                  -> cache:products:cursor:all:start:20
?afterId=20&pageSize=20                       -> cache:products:cursor:all:20:20
?afterId=40&pageSize=20&category=electronics  -> cache:products:cursor:electronics:40:20
```
Like the page cache, cursor keys **self-expire via TTL** (no registry set) and category is
normalized to lower-case so `Electronics`/`electronics` share a key.

### Index recommendation
Keyset filters + orders on `(Category, Id)`. The current index is single-column `Category`, which
still works (the PK clustered index on `Id` assists the seek). For **optimal** performance a
**composite `(Category, Id)`** index is recommended — this should be added via a **dedicated,
reviewed EF migration** (kept out of the pagination change to avoid scaffolding unrelated model
drift).

### Cache keys
| Key pattern | Type | Written by | Purpose / TTL |
|-------------|------|-----------|---------------|
| `cache:products:cursor:{category\|all}:{afterId\|start}:{pageSize}` | STRING (JSON) | `GetProductsByCursorAsync` (miss) | One keyset batch (items + nextCursor); TTL 5m |

### Trade-off (why not use keyset everywhere)
| | Offset (`/api/products`) | Keyset (`/api/products/feed`) |
|---|---|---|
| Deep-page speed | degrades with depth | **constant** |
| Jump to arbitrary page ("Page 500") | **yes** | no (sequential only) |
| Total count / "of 5,000" | easy (`TotalCount`) | not returned (would need a cached count) |
| Numbered page buttons | **ideal** | not suitable |
| "Load more" / infinite scroll | works | **ideal** |

> Both endpoints coexist — offset for any numbered-page UI, keyset for the "Load more" feed. The
> keyset path never runs a `COUNT(*)`, and reads a constant ~21 rows per batch at any depth.

---

## 18. Azure Managed Redis — Passwordless Connection (Entra ID)

The app connects to **Azure Managed Redis** using **Microsoft Entra ID** authentication via
`DefaultAzureCredential` — **no access keys or connection-string secrets** anywhere.

### Why passwordless
- **No secret to leak** — access keys are **disabled** on the cache; only identity-based auth works.
- **Consistent with the rest of the stack** — the app already uses `DefaultAzureCredential` for
  Key Vault; Redis now uses the same identity model.
- **Automatic token refresh** — `Microsoft.Azure.StackExchangeRedis` refreshes the Entra token
  before expiry, so long-lived connections don't drop.

### Configuration
Only the **host name** is stored (not a secret — auth is identity-based), in `appsettings.json`:
```json
"Redis": { "HostName": "ecommerce-redis-animesh.westus.redis.azure.net" }
```
> There is **no** `ConnectionStrings:Redis` and **no** `ConnectionStrings--Redis` Key Vault secret —
> both were removed when moving to passwordless. Azure Managed Redis uses **port 10000 + TLS**
> (classic Azure Cache for Redis used 6380).

### Connection setup (`Program.cs`)
```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisHostName = builder.Configuration["Redis:HostName"];
    if (string.IsNullOrEmpty(redisHostName))
    {
        // Local dev fallback: raw connection string (e.g. "localhost:6379")
        return ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!);
    }

    var options = ConfigurationOptions.Parse($"{redisHostName}:10000");
    options.Ssl = true;
    options.AbortOnConnectFail = false;   // resilient: keep retrying, don't crash on startup
    options.ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential())
           .GetAwaiter().GetResult();      // enables Entra ID auth + auto token refresh
    return ConnectionMultiplexer.Connect(options);
});
```
`ConfigureForAzureWithTokenCredentialAsync` is an extension from the
**`Microsoft.Azure.StackExchangeRedis`** package.

### Identity ? data access (required)
Entra auth needs a **data-plane** access-policy assignment on the cache — being subscription
**Owner** is *not* enough (that's control-plane only):
- **Local dev:** assign your `az login` user the **Data Owner** (`+@all ~*`) access policy on the
  Redis instance (portal: *Access policies* / CLI:
  `az redisenterprise database access-policy-assignment`).
- **Azure deploy:** enable **Managed Identity** on the App Service / Container App and assign it
  the same Data Owner access policy.

### Health check
The Redis health check reuses the **same registered `IConnectionMultiplexer`**, so it authenticates
via Entra ID exactly like the app (no separate connection string):
```csharp
healthChecks.AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis");
```

### Cache settings (provisioned)
| Setting | Value | Note |
|---------|-------|------|
| Port / protocol | `10000` / TLS | Azure Managed Redis default |
| Access keys | **Disabled** | Entra ID only — no key to leak |
| Eviction policy | `VolatileLRU` | evicts only keys **with a TTL**; keys set **without** TTL (e.g. stock/reservation) are protected |
| Network | Public (dev) | lock down with a Private Endpoint for production |
| Geo-replication | Off | HA feature, not needed for dev |

> **Eviction & the hot path:** `VolatileLRU` only evicts keys that have an expiry. The cache-aside
> entries (`cache:products:*`, `cache:cart:*`) carry TTLs so they can be evicted safely, while
> authoritative `stock:{id}` / `reservation:*` keys created without a TTL are **not** evicted —
> preserving inventory correctness (Section 8). Any drift from a Redis restart/eviction is still
> self-healed by the **reconciliation** service (Section 12).

---

## 19. Azure Service Bus — Post-Payment Fulfillment Queue (passwordless)

A **message queue** now sits **between** the fast/critical settlement and the slow fulfillment
work. This decouples the invoice PDF + email (slow, externally-dependent) from stock settlement
(fast, must-be-correct), so a flaky email provider can never block inventory or the checkout
response.

### The split (Outbox -> relay -> Service Bus -> worker)
```
/payment/pay (success)
   -> ConfirmStockAsync : SQL txn { Order=Confirmed + OutboxMessage }   (atomic capture)
          |
   OutboxRelayService (polls SQL every 5s, distributed-locked):
      1. settle stock INLINE  (Redis Confirm + SQL deduct)   <- fast + critical, stays here
      2. MarkStockSettledAt   (idempotency guard)
      3. publish -> Service Bus queue "order-fulfillment" { orderId }
          |
   [ Azure Service Bus queue: order-fulfillment ]
          |
   FulfillmentWorker (ServiceBusProcessor, event-driven):
      4. generate invoice PDF
      5. send email
      6. persist invoice record
      (Complete on success; Abandon on failure -> redelivery -> DLQ after MaxDeliveryCount)
```

### Why keep the Outbox *and* add Service Bus
- **Outbox** guarantees the fulfillment intent is captured **atomically** with `Order=Confirmed`
  in the **same SQL transaction** — no lost events if the app crashes right after commit.
- **Service Bus** is the **reliable transport** for the slow work: independent retries, automatic
  **dead-letter queue**, and independent scaling of the consumer. The relay is just the bridge
  (SQL outbox -> broker).
- This is the exact "migration path" the Outbox section always pointed to — now implemented.

### Why stock settlement stays inline (not queued)
Settlement (Redis + SQL) is **fast and correctness-critical**. Keeping it in the relay means the
`stock:{id}` / SQL truth is finalized deterministically under the distributed lock, before any
message leaves. Only the **slow, retry-prone** work (PDF + external email) goes on the queue.

### Idempotency across the queue (at-least-once safe)
Service Bus delivers **at-least-once**, so the worker may run twice for one order. Safe because:
- Stock is settled **before** publish and guarded by `StockSettledAt` (Section 5, Layer 3) — a
  publish retry re-sends the message but never re-deducts stock.
- The message's `MessageId = orderId` gives a natural de-dupe / trace key.
- Fulfillment work is derived from the **persisted Order** (deterministic), so a rare double
  invoice/email is the worst case — not data corruption. (A processed-invoice guard can be added
  if strict once-only email is required.)

### Passwordless connection (Entra ID)
Consistent with Key Vault + Azure Managed Redis — **no connection string**. Only the namespace is
configured; auth is `DefaultAzureCredential`:
```json
"AzureServiceBus": {
  "FullyQualifiedNamespace": "ecommerce-sb-animesh.servicebus.windows.net",
  "FulfillmentQueueName": "order-fulfillment"
}
```
```csharp
// Program.cs — singleton client, passwordless
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
    return new ServiceBusClient(o.FullyQualifiedNamespace, new DefaultAzureCredential());
});
builder.Services.AddScoped<IFulfillmentPublisher, ServiceBusFulfillmentPublisher>();
```
**Required RBAC:** assign the identity **Azure Service Bus Data Owner** on the namespace
(local dev = your `az login` user; Azure = the app's Managed Identity). Being subscription Owner
is control-plane only and does **not** grant data-plane send/receive.

### Components
| Piece | Layer | Role |
|-------|-------|------|
| `AzureServiceBusOptions` | Application | Namespace + queue name (no secret) |
| `OrderFulfillmentMessage` | Application | Message contract (`OrderId`) |
| `IFulfillmentPublisher` | Application | Transport abstraction (testable) |
| `ServiceBusFulfillmentPublisher` | API | `ServiceBusSender` impl (passwordless) |
| `OutboxRelayService` | API (hosted) | Settle inline + publish |
| `FulfillmentWorker` | API (hosted) | `ServiceBusProcessor` -> invoice + email + persist |

### Queue settings (provisioned)
| Setting | Value | Note |
|---------|-------|------|
| Namespace | `ecommerce-sb-animesh` | **Basic** tier (queues only; topics need Standard) |
| Queue | `order-fulfillment` | Active |
| Max delivery count | `10` | Auto dead-letters after 10 failed deliveries |
| Auth | Entra ID (Data Owner) | Passwordless; no SAS connection string |

> **Tier note:** Basic supports **queues** (enough for this single-queue design). If you later
> split into per-step **topics + subscriptions** (settle / invoice / email as separate consumers),
> upgrade the namespace to **Standard**.

---

## Key Takeaways
1. **Redis = hot working counter** (helps at RESERVE); **SQL = durable truth**.
2. **Reserve** is the only high-concurrency point -> Redis Lua atomic prevents oversell.
3. **Dual-write** (STRING + ZSET) makes reservation-expiry reliable.
4. **Idempotency has 3 layers**: request (`[Idempotent]`), confirm (`Order.Status`),
   settlement (`StockSettledAt`) — safe for at-least-once (queue-ready).
5. **Outbox pattern** fixes the Redis<->SQL dual-write drift — atomic capture + reliable
   background settlement.
6. **3 hosted services** (sweeper, outbox, reconciliation) each guarded by a **distributed lock**.
7. **Reconciliation** is the self-healing safety net for drift the other mechanisms miss.
8. Consistency is **eventual**; hot-path correctness is **immediate**.
9. **Catalog read-cache** (cache-aside, `cache:products:*`, 5-min TTL) offloads read-heavy
   product/paged queries from SQL — separate from the `stock:{id}` hot path, so it never
   affects oversell correctness. Pages are per-category, case-insensitive, and self-expire.
10. **Cart read-cache** (cache-aside, `cache:cart:{userId}`, 10-min TTL) offloads per-user cart
    reads from SQL — no contention (cart is per-user), correctness kept by **invalidate-on-write**.
11. **Two-step flow**: `/checkout/begin` (reserve) -> `/payment/pay` (confirm/release) ->
    background worker does invoice + email + stock settlement — so the request path is fast.
12. **Multi-instance:** shared Redis is safe; background jobs need a **distributed lock**.
13. **Two pagination styles**: **offset** (`/api/products`, numbered pages) and **keyset/cursor**
    (`/api/products/feed`, "Load more"). Keyset seeks via `WHERE Id > cursor` — **constant** cost
    at any depth, no `COUNT(*)` — and is cache-aside keyed by cursor (`cache:products:cursor:*`).
14. **Passwordless Redis**: connects to **Azure Managed Redis** via **Entra ID**
    (`DefaultAzureCredential`, port 10000 + TLS, `Microsoft.Azure.StackExchangeRedis`). Access keys
    are **disabled**; only the non-secret `Redis:HostName` is configured. Needs a **Data Owner**
    access-policy assignment on the cache (Owner/IAM is control-plane only). See Section 18.
15. **Service Bus fulfillment queue**: the outbox **relay** settles stock inline then **publishes**
    to Azure Service Bus (`order-fulfillment`); the **FulfillmentWorker** consumes it for invoice +
    email + persist — independent retries + auto dead-letter. Passwordless (Entra ID, **Data
    Owner**), Outbox stays as the atomic capture. See Section 19.
