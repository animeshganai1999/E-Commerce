using System.Linq.Expressions;
using System.Reflection;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Application.Services;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Cart uses authenticated user and catalog pricing", CartUsesAuthenticatedUserAndCatalogPricing),
    ("Checkout uses authenticated user and catalog pricing", CheckoutUsesAuthenticatedUserAndCatalogPricing),
    ("Checkout confirmation rejects another user's order", ConfirmationRejectsAnotherUsersOrder),
    ("Duplicate confirmation is idempotent", DuplicateConfirmationIsIdempotent),
    ("Repeated failed payment releases stock once", RepeatedFailedPaymentReleasesStockOnce),
    ("Expired confirmation releases stock and conflicts", ExpiredConfirmationReleasesStockAndConflicts),
    ("Redis release script is conditional", RedisReleaseScriptIsConditional)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL: {test.Name}{Environment.NewLine}{ex}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} verification(s) failed.");
    return 1;
}

Console.WriteLine($"All {tests.Length} security and financial correctness verifications passed.");
return 0;

static async Task CartUsesAuthenticatedUserAndCatalogPricing()
{
    var authenticatedUserId = Guid.NewGuid();
    var clientSuppliedUserId = Guid.NewGuid();
    var cartRepository = new FakeCartRepository();
    var productRepository = new FakeProductRepository(
        new Product { Id = 7, Title = "Canonical title", Price = 125.50m, StockQuantity = 20 });
    var service = new CartService(cartRepository, new FakeCartCache(), productRepository);

    var request = new CartDiffDTO
    {
        Added =
        [
            new CartItemDTO { ProductId = 7, Quantity = 2 }
        ]
    };

    await service.ApplyCartDiffAsync(authenticatedUserId, request);

    var saved = AssertSingle(cartRepository.Items);
    AssertEqual(authenticatedUserId, saved.UserId, "The authenticated user must own the cart item.");
    AssertNotEqual(clientSuppliedUserId, saved.UserId, "A client-controlled user id must not be used.");
    AssertEqual(125.50m, saved.UnitPrice, "The catalog price must be persisted.");
    AssertEqual("Canonical title", saved.Description, "The catalog title must be persisted.");
}

static async Task CheckoutUsesAuthenticatedUserAndCatalogPricing()
{
    var authenticatedUserId = Guid.NewGuid();
    var cartRepository = new FakeCartRepository(
        new CartItem
        {
            Id = 1,
            UserId = authenticatedUserId,
            ProductId = 3,
            Quantity = 2,
            UnitPrice = 0.01m,
            Description = "Client supplied title"
        });
    var productRepository = new FakeProductRepository(
        new Product { Id = 3, Title = "Server title", Price = 49.99m, StockQuantity = 10 });
    var orderRepository = new FakeOrderRepository();
    var stockRepository = new FakeStockReservationRepository(new Dictionary<int, long> { [3] = 10 });
    var service = new CheckoutService(
        cartRepository,
        orderRepository,
        stockRepository,
        productRepository,
        new FakeCartCache());

    var result = await service.BeginCheckoutAsync(
        authenticatedUserId,
        new BeginCheckoutModel { OrderDetails = CreateOrderDetails() });

    var order = orderRepository.Order ?? throw new Exception("The pending order was not persisted.");
    var line = AssertSingle(order.Items);
    AssertEqual(authenticatedUserId, order.UserId, "The authenticated user must own the order.");
    AssertEqual(99.98m, order.TotalAmount, "The order total must use the catalog price.");
    AssertEqual(49.99m, line.UnitPrice, "The order line must use the catalog price.");
    AssertEqual("Server title", line.Description, "The order line must use the catalog title.");
    AssertEqual(order.Id, result.OrderId, "The returned order id must match the persisted order.");
}

static async Task ConfirmationRejectsAnotherUsersOrder()
{
    var ownerId = Guid.NewGuid();
    var attackerId = Guid.NewGuid();
    var orderRepository = new FakeOrderRepository
    {
        Order = CreateOrder(ownerId)
    };
    var service = CreateCheckoutService(orderRepository, new FakeStockReservationRepository());

    await AssertThrowsAsync<ForbiddenAccessException>(
        () => service.ConfirmStockAsync(orderRepository.Order.Id, attackerId));

    AssertEqual(0, orderRepository.ConfirmCalls, "The repository transition must not run for another user.");
}

static async Task DuplicateConfirmationIsIdempotent()
{
    var ownerId = Guid.NewGuid();
    var orderRepository = new FakeOrderRepository
    {
        Order = CreateOrder(ownerId),
        ConfirmResult = OrderTransitionResult.AlreadyConfirmed
    };
    var service = CreateCheckoutService(orderRepository, new FakeStockReservationRepository());

    await service.ConfirmStockAsync(orderRepository.Order.Id, ownerId);

    AssertEqual(1, orderRepository.ConfirmCalls, "The duplicate confirmation should be checked once.");
    AssertEqual("OrderConfirmed", orderRepository.LastOutboxMessage?.Type,
        "Confirmation must use the expected outbox event.");
}

static async Task RepeatedFailedPaymentReleasesStockOnce()
{
    var ownerId = Guid.NewGuid();
    var order = CreateOrder(ownerId);
    var orderRepository = new FakeOrderRepository
    {
        Order = order,
        FailResults = new Queue<OrderTransitionResult>(
        [
            OrderTransitionResult.Succeeded,
            OrderTransitionResult.AlreadyFailed
        ])
    };
    var stockRepository = new FakeStockReservationRepository(new Dictionary<int, long> { [5] = 8 });
    stockRepository.AddReservation(order.Id, 5, 2);
    var service = CreateCheckoutService(orderRepository, stockRepository);

    await service.ReleaseStockAsync(order.Id, ownerId);
    await service.ReleaseStockAsync(order.Id, ownerId);

    AssertEqual(10L, await stockRepository.GetStockAsync(5),
        "Repeated failure handling must return reserved stock only once.");
}

static async Task ExpiredConfirmationReleasesStockAndConflicts()
{
    var ownerId = Guid.NewGuid();
    var order = CreateOrder(ownerId);
    var orderRepository = new FakeOrderRepository
    {
        Order = order,
        ConfirmResult = OrderTransitionResult.Expired
    };
    var stockRepository = new FakeStockReservationRepository(new Dictionary<int, long> { [5] = 8 });
    stockRepository.AddReservation(order.Id, 5, 2);
    var service = CreateCheckoutService(orderRepository, stockRepository);

    await AssertThrowsAsync<OrderStateConflictException>(
        () => service.ConfirmStockAsync(order.Id, ownerId));

    AssertEqual(10L, await stockRepository.GetStockAsync(5),
        "An expired order must return its reservation.");
}

static Task RedisReleaseScriptIsConditional()
{
    var field = typeof(StockReservationRepository)
        .GetField("ReleaseScript", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception("ReleaseScript was not found.");
    var script = field.GetRawConstantValue() as string
        ?? throw new Exception("ReleaseScript is not a constant string.");

    var readIndex = script.IndexOf("GET", StringComparison.Ordinal);
    var incrementIndex = script.IndexOf("INCRBY", StringComparison.Ordinal);
    Assert(readIndex >= 0 && readIndex < incrementIndex,
        "The release script must read the reservation before incrementing stock.");
    Assert(script.Contains("return 0", StringComparison.Ordinal),
        "The release script must no-op when the reservation is absent.");

    return Task.CompletedTask;
}

static CheckoutService CreateCheckoutService(
    FakeOrderRepository orderRepository,
    FakeStockReservationRepository stockRepository)
{
    return new CheckoutService(
        new FakeCartRepository(),
        orderRepository,
        stockRepository,
        new FakeProductRepository(),
        new FakeCartCache());
}

static Order CreateOrder(Guid userId)
{
    var orderId = Guid.NewGuid();
    return new Order
    {
        Id = orderId,
        UserId = userId,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        ReservationExpiresAt = DateTime.UtcNow.AddMinutes(10),
        Items =
        [
            new OrderLineItem
            {
                OrderId = orderId,
                ProductId = 5,
                Quantity = 2,
                UnitPrice = 10,
                Description = "Product"
            }
        ]
    };
}

static OrderDetails CreateOrderDetails() =>
    new()
    {
        FirstName = "Test",
        LastName = "User",
        Email = "test@example.com",
        Address = "1 Test Street",
        Address2 = string.Empty,
        Country = "India",
        State = "WB",
        Zip = "700001"
    };

static T AssertSingle<T>(IEnumerable<T> values)
{
    var list = values.ToList();
    if (list.Count != 1)
        throw new Exception($"Expected one item but found {list.Count}.");
    return list[0];
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{message} Expected: {expected}; actual: {actual}.");
}

static void AssertNotEqual<T>(T unexpected, T actual, string message)
{
    if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        throw new Exception($"{message} Unexpected value: {unexpected}.");
}

static async Task AssertThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new Exception($"Expected {typeof(TException).Name}.");
}

sealed class FakeCartRepository : ICartRepository
{
    public List<CartItem> Items { get; } = [];

    public FakeCartRepository(params CartItem[] items)
    {
        Items.AddRange(items);
    }

    public Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId) =>
        Task.FromResult<IEnumerable<CartItem>>(Items.Where(item => item.UserId == userId).ToList());

    public Task AddRangeAsync(IEnumerable<CartItem> entities)
    {
        Items.AddRange(entities);
        return Task.CompletedTask;
    }

    public Task<CartItem?> GetAsync(Expression<Func<CartItem, bool>> filter) =>
        Task.FromResult(Items.AsQueryable().FirstOrDefault(filter));

    public Task<CartItem> GetByIdAsync(int id) =>
        Task.FromResult(Items.Single(item => item.Id == id));

    public Task<IEnumerable<CartItem>> GetAllAsync() =>
        Task.FromResult<IEnumerable<CartItem>>(Items.ToList());

    public Task AddAsync(CartItem entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CartItem entity) => Task.CompletedTask;

    public Task DeleteAsync(Expression<Func<CartItem, bool>> filter)
    {
        Items.RemoveAll(item => filter.Compile()(item));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}

sealed class FakeCartCache : ICartCache
{
    public Task<List<CartItem>?> GetByUserAsync(Guid userId) =>
        Task.FromResult<List<CartItem>?>(null);

    public Task SetByUserAsync(Guid userId, IEnumerable<CartItem> items) =>
        Task.CompletedTask;

    public Task InvalidateAsync(Guid userId) => Task.CompletedTask;
}

sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<int, Product> _products;

    public FakeProductRepository(params Product[] products)
    {
        _products = products.ToDictionary(product => product.Id);
    }

    public Task<List<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds) =>
        Task.FromResult(productIds.Distinct()
            .Where(_products.ContainsKey)
            .Select(id => _products[id])
            .ToList());

    public Task<Product?> GetProductByIdAsync(int id) =>
        Task.FromResult(_products.GetValueOrDefault(id));

    public Task<int?> GetStockFromSqlAsync(int productId) =>
        Task.FromResult(_products.TryGetValue(productId, out var product)
            ? (int?)product.StockQuantity
            : null);

    public Task<List<(int Id, int Stock)>> GetStockForManyAsync(IEnumerable<int> productIds) =>
        Task.FromResult(productIds
            .Where(_products.ContainsKey)
            .Select(id => (id, _products[id].StockQuantity))
            .ToList());

    public Task<bool> TryDeductStockAsync(int productId, int quantity) =>
        throw new NotSupportedException();

    public Task<IEnumerable<Product>> GetAllProductsAsync() =>
        Task.FromResult<IEnumerable<Product>>(_products.Values.ToList());

    public Task<(List<Product> Items, int TotalCount)> GetProductsPageAsync(
        int page,
        int pageSize,
        string? category = null) =>
        throw new NotSupportedException();

    public Task<(List<Product> Items, int? NextCursor)> GetProductsByCursorAsync(
        int? afterId,
        int pageSize,
        string? category = null) =>
        throw new NotSupportedException();

    public Task<Product> GetByIdAsync(int id) =>
        Task.FromResult(_products[id]);

    public Task<IEnumerable<Product>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Product>>(_products.Values.ToList());

    public Task AddAsync(Product entity)
    {
        _products[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product entity)
    {
        _products[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Expression<Func<Product, bool>> filter) =>
        throw new NotSupportedException();
}

sealed class FakeOrderRepository : IOrderRepository
{
    public Order? Order { get; set; }
    public OrderTransitionResult ConfirmResult { get; set; } = OrderTransitionResult.Succeeded;
    public Queue<OrderTransitionResult> FailResults { get; set; } = [];
    public int ConfirmCalls { get; private set; }
    public OutboxMessage? LastOutboxMessage { get; private set; }

    public Task AddAsync(Order order)
    {
        Order = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid orderId) =>
        Task.FromResult(Order?.Id == orderId ? Order : null);

    public Task<OrderTransitionResult> ConfirmWithOutboxAsync(
        Guid orderId,
        Guid userId,
        DateTime confirmedAt,
        OutboxMessage outboxMessage)
    {
        ConfirmCalls++;
        LastOutboxMessage = outboxMessage;
        return Task.FromResult(ConfirmResult);
    }

    public Task<OrderTransitionResult> FailAsync(Guid orderId, Guid userId)
    {
        var result = FailResults.Count > 0
            ? FailResults.Dequeue()
            : OrderTransitionResult.Succeeded;
        return Task.FromResult(result);
    }

    public Task UpdateStatusAsync(Guid orderId, OrderStatus status, DateTime? confirmedAt = null) =>
        Task.CompletedTask;

    public Task MarkStockSettledAsync(Guid orderId, DateTime settledAt) =>
        Task.CompletedTask;

    public Task<Dictionary<int, int>> GetPendingReservedQuantitiesAsync() =>
        Task.FromResult(new Dictionary<int, int>());

    public Task<Dictionary<int, int>> GetPendingReservedQuantitiesForAsync(IEnumerable<int> productIds) =>
        Task.FromResult(new Dictionary<int, int>());

    public Task<ExpiredReservationAction> ResolveExpiredReservationAsync(Guid orderId, DateTime asOfUtc) =>
        Task.FromResult(Order?.Status == OrderStatus.Confirmed
            ? ExpiredReservationAction.Confirm
            : ExpiredReservationAction.Release);

    public Task<List<Order>> GetExpiredPendingOrdersAsync(DateTime asOfUtc) =>
        Task.FromResult(new List<Order>());

    public Task SaveChangesAsync() => Task.CompletedTask;
}

sealed class FakeStockReservationRepository : IStockReservationRepository
{
    private readonly Dictionary<int, long> _stock;
    private readonly Dictionary<(Guid OrderId, int ProductId), int> _reservations = [];

    public FakeStockReservationRepository(Dictionary<int, long>? stock = null)
    {
        _stock = stock ?? [];
    }

    public void AddReservation(Guid orderId, int productId, int quantity)
    {
        _reservations[(orderId, productId)] = quantity;
    }

    public Task<ReserveResult> TryReserveAsync(
        Guid orderId,
        int productId,
        int quantity,
        TimeSpan ttl)
    {
        if (!_stock.TryGetValue(productId, out var available))
            return Task.FromResult(ReserveResult.StockMissing);
        if (available < quantity)
            return Task.FromResult(ReserveResult.Insufficient);

        _stock[productId] = available - quantity;
        _reservations[(orderId, productId)] = quantity;
        return Task.FromResult(ReserveResult.Success);
    }

    public Task ConfirmAsync(Guid orderId, int productId, int quantity)
    {
        _reservations.Remove((orderId, productId));
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(Guid orderId, int productId, int quantity)
    {
        if (_reservations.Remove((orderId, productId), out var reservedQuantity))
            _stock[productId] = _stock.GetValueOrDefault(productId) + reservedQuantity;

        return Task.CompletedTask;
    }

    public Task PopulateStockIfAbsentAsync(int productId, int quantity, TimeSpan? idleTtl = null)
    {
        _stock.TryAdd(productId, quantity);
        return Task.CompletedTask;
    }

    public Task<long?> GetStockAsync(int productId) =>
        Task.FromResult(_stock.TryGetValue(productId, out var value) ? (long?)value : null);

    public Task PreloadStockAsync(int productId, int quantity)
    {
        _stock[productId] = quantity;
        return Task.CompletedTask;
    }

    public Task<int> ReclaimExpiredAsync(
        Func<Guid, Task<ExpiredReservationAction>> resolveActionAsync) =>
        Task.FromResult(0);
    public Task<string?> AcquireLockAsync(string lockKey, TimeSpan ttl) => Task.FromResult<string?>("token");
    public Task ReleaseLockAsync(string lockKey, string token) => Task.CompletedTask;
    public Task WarmUpAsync(IEnumerable<(int Id, int Stock)> items) => Task.CompletedTask;

    public Task SetStockAsync(int productId, int quantity)
    {
        _stock[productId] = quantity;
        return Task.CompletedTask;
    }

    public Task<bool> SetStockIfUnchangedAsync(
        int productId,
        long expectedCurrent,
        int quantity)
    {
        if (!_stock.TryGetValue(productId, out var current) || current != expectedCurrent)
            return Task.FromResult(false);

        _stock[productId] = quantity;
        return Task.FromResult(true);
    }

    public Task<List<int>> GetTrackedProductIdsAsync() =>
        Task.FromResult(_stock.Keys.ToList());

    public Task FlushAllAsync()
    {
        _stock.Clear();
        _reservations.Clear();
        return Task.CompletedTask;
    }
}
