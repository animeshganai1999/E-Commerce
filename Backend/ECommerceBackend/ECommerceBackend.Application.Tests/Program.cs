using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using ECommerceBackend.API.Filters;
using ECommerceBackend.API.Controllers;
using ECommerceBackend.API.HostedServices;
using ECommerceBackend.Application.Constants;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Messaging;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Application.Options;
using ECommerceBackend.Application.Services;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Cart uses authenticated user and catalog pricing", CartUsesAuthenticatedUserAndCatalogPricing),
    ("Checkout uses authenticated user and catalog pricing", CheckoutUsesAuthenticatedUserAndCatalogPricing),
    ("Checkout confirmation rejects another user's order", ConfirmationRejectsAnotherUsersOrder),
    ("Checkout failure rejects another user's order", FailureRejectsAnotherUsersOrder),
    ("Duplicate confirmation is idempotent", DuplicateConfirmationIsIdempotent),
    ("Concurrent duplicate confirmation is idempotent", ConcurrentDuplicateConfirmationIsIdempotent),
    ("Repeated failed payment releases stock once", RepeatedFailedPaymentReleasesStockOnce),
    ("Expired confirmation releases stock and conflicts", ExpiredConfirmationReleasesStockAndConflicts),
    ("Redis release script is conditional", RedisReleaseScriptIsConditional),
    ("Cart rejects invalid product and quantity", CartRejectsInvalidProductAndQuantity),
    ("Cart rejects adding an existing product", CartRejectsAddingExistingProduct),
    ("Cart enforces distinct product limit", CartEnforcesDistinctProductLimit),
    ("Missing reservation cannot confirm order", MissingReservationCannotConfirmOrder),
    ("Order terminal transitions are one-way", OrderTerminalTransitionsAreOneWay),
    ("Authenticated controllers use JWT identity", AuthenticatedControllersUseJwtIdentity),
    ("Authenticated write models expose no user or price fields", AuthenticatedWriteModelsAreServerOwned),
    ("Publication retry does not settle stock twice", PublicationRetryDoesNotSettleStockTwice),
    ("Duplicate fulfillment skips completed work", DuplicateFulfillmentSkipsCompletedWork),
    ("Failed outbox messages remain recoverable", FailedOutboxMessagesRemainRecoverable),
    ("Concurrent identical requests execute once", ConcurrentIdenticalRequestsExecuteOnce),
    ("Idempotency key rejects a changed payload", IdempotencyKeyRejectsChangedPayload),
    ("Completed idempotent response replays exactly", CompletedResponseReplaysExactly),
    ("Idempotency keys are isolated by user and route", IdempotencyKeysAreScoped),
    ("Failed idempotent requests can retry", FailedIdempotentRequestsCanRetry),
    ("Request representation is fingerprinted", RequestRepresentationIsFingerprinted),
    ("Invalid idempotency keys are rejected", InvalidIdempotencyKeysAreRejected),
    ("Server errors are not cached", ServerErrorsAreNotCached),
    ("Login and registration persist only token hashes", AuthTests.IssuanceStoresHashesAsync),
    ("Refresh persists the replacement hash and rejects invalid tokens", AuthTests.RotationContractAsync),
    ("Cookie authentication checks trusted origins and clears logout cookies", AuthTests.CookieContractAsync)
};

if (args.Contains("--auth-sql"))
    tests = [.. tests, ("SQL refresh rotation, migration, concurrency and HTTP lifecycle", AuthSqlTests.RunAsync)];

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

static async Task FailureRejectsAnotherUsersOrder()
{
    var ownerId = Guid.NewGuid();
    var attackerId = Guid.NewGuid();
    var orderRepository = new FakeOrderRepository
    {
        Order = CreateOrder(ownerId)
    };
    var service = CreateCheckoutService(orderRepository, new FakeStockReservationRepository());

    await AssertThrowsAsync<ForbiddenAccessException>(
        () => service.ReleaseStockAsync(orderRepository.Order.Id, attackerId));

    AssertEqual(0, orderRepository.FailCalls,
        "The failure transition must not run for another user.");
}

static async Task DuplicateConfirmationIsIdempotent()
{
    var ownerId = Guid.NewGuid();
    var orderRepository = new FakeOrderRepository
    {
        Order = CreateOrder(ownerId),
        ConfirmResult = OrderTransitionResult.AlreadyConfirmed
    };
    orderRepository.Order.Status = OrderStatus.Confirmed;
    var service = CreateCheckoutService(orderRepository, new FakeStockReservationRepository());

    await service.ConfirmStockAsync(orderRepository.Order.Id, ownerId);

    AssertEqual(0, orderRepository.ConfirmCalls,
        "A duplicate confirmation must not execute another transition.");
    AssertEqual<OutboxMessage?>(null, orderRepository.LastOutboxMessage,
        "A duplicate confirmation must not create another outbox event.");
}

static async Task ConcurrentDuplicateConfirmationIsIdempotent()
{
    var ownerId = Guid.NewGuid();
    var order = CreateOrder(ownerId);
    var orderRepository = new FakeOrderRepository
    {
        Order = order,
        ConfirmResult = OrderTransitionResult.AlreadyConfirmed
    };
    var stockRepository = new FakeStockReservationRepository();
    stockRepository.AddReservation(order.Id, 5, 2);
    var service = CreateCheckoutService(orderRepository, stockRepository);

    await service.ConfirmStockAsync(order.Id, ownerId);

    AssertEqual(1, orderRepository.ConfirmCalls,
        "A concurrent duplicate must reach the conditional repository transition once.");
    AssertEqual("OrderConfirmed", orderRepository.LastOutboxMessage?.Type,
        "The attempted transition must use the confirmation event type.");
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

    var confirmField = typeof(StockReservationRepository)
        .GetField("ConfirmScript", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception("ConfirmScript was not found.");
    var confirmScript = confirmField.GetRawConstantValue() as string
        ?? throw new Exception("ConfirmScript is not a constant string.");
    Assert(confirmScript.Contains("GET", StringComparison.Ordinal),
        "Confirmation must inspect the reservation key.");
    Assert(confirmScript.Contains("return 0", StringComparison.Ordinal),
        "Confirmation must report a missing reservation without mutating it.");

    return Task.CompletedTask;
}

static async Task CartRejectsInvalidProductAndQuantity()
{
    var userId = Guid.NewGuid();
    var service = new CartService(
        new FakeCartRepository(),
        new FakeCartCache(),
        new FakeProductRepository());

    await AssertThrowsAsync<RequestValidationException>(() =>
        service.ApplyCartDiffAsync(
            userId,
            new CartDiffDTO
            {
                Added =
                [
                    new CartItemDTO
                    {
                        ProductId = 99,
                        Quantity = CartPolicy.MaxQuantityPerItem + 1
                    }
                ]
            }));

    await AssertThrowsAsync<RequestValidationException>(() =>
        service.ApplyCartDiffAsync(
            userId,
            new CartDiffDTO
            {
                Updated = [new CartItemDTO { ProductId = 99, Quantity = 1 }]
            }));
}

static async Task CartRejectsAddingExistingProduct()
{
    var userId = Guid.NewGuid();
    var service = new CartService(
        new FakeCartRepository(
            new CartItem
            {
                UserId = userId,
                ProductId = 7,
                Quantity = CartPolicy.MaxQuantityPerItem,
                UnitPrice = 1,
                Description = "Existing"
            }),
        new FakeCartCache(),
        new FakeProductRepository(
            new Product
            {
                Id = 7,
                Title = "Existing",
                Price = 1,
                StockQuantity = 1000
            }));

    await AssertThrowsAsync<RequestValidationException>(() =>
        service.ApplyCartDiffAsync(
            userId,
            new CartDiffDTO
            {
                Added =
                [
                    new CartItemDTO
                    {
                        ProductId = 7,
                        Quantity = CartPolicy.MaxQuantityPerItem
                    }
                ]
            }));
}

static async Task CartEnforcesDistinctProductLimit()
{
    var userId = Guid.NewGuid();
    var existingItems = Enumerable.Range(1, CartPolicy.MaxDistinctItems)
        .Select(productId => new CartItem
        {
            UserId = userId,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 1,
            Description = $"Product {productId}"
        })
        .ToArray();
    var service = new CartService(
        new FakeCartRepository(existingItems),
        new FakeCartCache(),
        new FakeProductRepository(
            new Product
            {
                Id = CartPolicy.MaxDistinctItems + 1,
                Title = "Extra product",
                Price = 1,
                StockQuantity = 1
            }));

    await AssertThrowsAsync<RequestValidationException>(() =>
        service.ApplyCartDiffAsync(
            userId,
            new CartDiffDTO
            {
                Added =
                [
                    new CartItemDTO
                    {
                        ProductId = CartPolicy.MaxDistinctItems + 1,
                        Quantity = 1
                    }
                ]
            }));
}

static async Task MissingReservationCannotConfirmOrder()
{
    var ownerId = Guid.NewGuid();
    var orderRepository = new FakeOrderRepository
    {
        Order = CreateOrder(ownerId)
    };
    var service = CreateCheckoutService(orderRepository, new FakeStockReservationRepository());

    await AssertThrowsAsync<OrderStateConflictException>(
        () => service.ConfirmStockAsync(orderRepository.Order.Id, ownerId));

    AssertEqual(0, orderRepository.ConfirmCalls,
        "An order without its Redis reservation must not be confirmed or create an outbox event.");
}

static Task OrderTerminalTransitionsAreOneWay()
{
    var confirmed = CreateOrder(Guid.NewGuid());
    AssertEqual(OrderTransitionResult.Succeeded, confirmed.TryConfirm(DateTime.UtcNow),
        "A pending order must be confirmable.");
    AssertEqual(OrderTransitionResult.AlreadyConfirmed, confirmed.TryFail(),
        "A confirmed order must not transition to failed.");
    AssertEqual(OrderStatus.Confirmed, confirmed.Status,
        "A failed callback must not change a confirmed order.");

    var failed = CreateOrder(Guid.NewGuid());
    AssertEqual(OrderTransitionResult.Succeeded, failed.TryFail(),
        "A pending order must be fail-able.");
    AssertEqual(OrderTransitionResult.AlreadyFailed, failed.TryConfirm(DateTime.UtcNow),
        "A failed order must not transition to confirmed.");
    AssertEqual(OrderStatus.Failed, failed.Status,
        "A successful callback must not change a failed order.");

    return Task.CompletedTask;
}

static async Task AuthenticatedControllersUseJwtIdentity()
{
    var userId = Guid.NewGuid();
    var cartService = new RecordingCartService();
    var cartController = new CartController(
        NullLogger<CartController>.Instance,
        null!,
        cartService);
    SetAuthenticatedUser(cartController, userId);
    await cartController.UpdateCart(new CartDiffDTO());
    await cartController.GetCart();
    AssertEqual(userId, cartService.LastUserId,
        "Cart endpoints must use the JWT user identifier.");

    var checkoutService = new RecordingCheckoutService();
    var checkoutController = new CheckoutController(checkoutService);
    SetAuthenticatedUser(checkoutController, userId);
    await checkoutController.Begin(
        new BeginCheckoutModel { OrderDetails = CreateOrderDetails() });
    AssertEqual(userId, checkoutService.LastUserId,
        "Checkout must use the JWT user identifier.");

    var paymentController = new PaymentController(
        checkoutService,
        NullLogger<PaymentController>.Instance);
    SetAuthenticatedUser(paymentController, userId);
    await paymentController.Pay(new PaymentRequest { OrderId = Guid.NewGuid(), Success = true });
    AssertEqual(userId, checkoutService.LastUserId,
        "Successful payment must use the JWT user identifier.");
    await paymentController.Pay(new PaymentRequest { OrderId = Guid.NewGuid(), Success = false });
    AssertEqual(userId, checkoutService.LastUserId,
        "Failed payment must use the JWT user identifier.");

    var orderedItemService = new RecordingOrderedItemService();
    var orderedItemsController = new OrderedItemsController(orderedItemService);
    SetAuthenticatedUser(orderedItemsController, userId);
    await orderedItemsController.GetInvoiceByUserId();
    AssertEqual(userId, orderedItemService.LastUserId,
        "Invoice reads must use the JWT user identifier.");
}

static Task AuthenticatedWriteModelsAreServerOwned()
{
    var cartProperties = typeof(CartItemDTO).GetProperties()
        .Select(property => property.Name)
        .OrderBy(name => name)
        .ToArray();
    AssertEqual("ProductId,Quantity", string.Join(',', cartProperties),
        "Cart writes must accept only product id and quantity.");

    Assert(typeof(BeginCheckoutModel).GetProperty("UserId") is null,
        "Checkout requests must not accept a user id.");
    Assert(typeof(PaymentRequest).GetProperty("UserId") is null,
        "Payment requests must not accept a user id.");

    return Task.CompletedTask;
}

static async Task PublicationRetryDoesNotSettleStockTwice()
{
    var order = CreateOrder(Guid.NewGuid());
    order.Status = OrderStatus.Confirmed;
    var orderRepository = new FakeOrderRepository { Order = order };
    var stockRepository = new FakeStockReservationRepository();
    stockRepository.AddReservation(order.Id, 5, 2);
    var publisher = new RecordingFulfillmentPublisher();
    var message = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        AggregateId = order.Id,
        Type = "OrderConfirmed",
        Payload = "{}",
        CreatedAt = DateTime.UtcNow
    };

    var method = typeof(OutboxRelayService).GetMethod(
        "SettleAndPublishAsync",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception("SettleAndPublishAsync was not found.");

    await InvokeTaskAsync(
        method,
        null,
        message,
        stockRepository,
        orderRepository,
        publisher,
        CancellationToken.None);
    await InvokeTaskAsync(
        method,
        null,
        message,
        stockRepository,
        orderRepository,
        publisher,
        CancellationToken.None);

    AssertEqual(1, orderRepository.CompletedSettlements,
        "A publication retry must not deduct SQL stock twice.");
    AssertEqual(2, publisher.PublishCalls,
        "Publication must still be attempted after stock is already settled.");
}

static async Task DuplicateFulfillmentSkipsCompletedWork()
{
    var order = CreateOrder(Guid.NewGuid());
    order.Status = OrderStatus.Confirmed;
    order.TotalAmount = 20;
    order.Email = "buyer@example.com";
    var orderRepository = new FakeOrderRepository { Order = order };
    var checkoutService = new RecordingCheckoutService();
    var emailService = new RecordingEmailService();
    var orderedItemService = new RecordingOrderedItemService();
    var services = new ServiceCollection()
        .AddSingleton<IOrderRepository>(orderRepository)
        .AddSingleton<IStockReservationRepository>(new FakeStockReservationRepository())
        .AddSingleton<ICheckoutService>(checkoutService)
        .AddSingleton<IEmailService>(emailService)
        .AddSingleton<IOrderedItemService>(orderedItemService)
        .BuildServiceProvider();
    var worker = new FulfillmentWorker(
        null!,
        services,
        NullLogger<FulfillmentWorker>.Instance,
        new ConfigurationBuilder().Build(),
        Options.Create(new AzureServiceBusOptions()));
    var method = typeof(FulfillmentWorker).GetMethod(
        "FulfillAsync",
        BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new Exception("FulfillAsync was not found.");

    await InvokeTaskAsync(method, worker, order.Id, CancellationToken.None);
    await InvokeTaskAsync(method, worker, order.Id, CancellationToken.None);

    AssertEqual(1, orderedItemService.UploadCalls,
        "Duplicate fulfillment must reuse the deterministic invoice blob.");
    AssertEqual(1, emailService.SendCalls,
        "Completed email delivery must not be repeated.");
    AssertEqual(1, orderedItemService.FulfilledCalls,
        "A fulfilled order must not be completed twice.");
    AssertEqual(order.TotalAmount, orderedItemService.Invoice?.TotalAmount,
        "The invoice total must use the order's canonical total.");
}

static Task FailedOutboxMessagesRemainRecoverable()
{
    var message = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        AggregateId = Guid.NewGuid(),
        Type = "OrderConfirmed",
        Payload = "{}",
        CreatedAt = DateTime.UtcNow
    };

    for (var attempt = 0; attempt < 10; attempt++)
        message.RecordFailure("Publish failed.", DateTime.UtcNow, 10);

    Assert(message.FailedAt.HasValue,
        "Retry-exhausted messages must remain in a visible terminal failure state.");
    AssertEqual(10, message.RetryCount,
        "The terminal outbox state must preserve its retry count.");

    var requeuedAt = DateTime.UtcNow;
    message.Requeue(requeuedAt);
    AssertEqual<DateTime?>(null, message.FailedAt,
        "A failed outbox message must be requeueable.");
    AssertEqual<DateTime?>(requeuedAt, message.NextAttemptAt,
        "A requeued message must become eligible at the requested time.");
    AssertEqual(0, message.RetryCount,
        "Manual requeue must restart automatic retry accounting.");

    return Task.CompletedTask;
}

static async Task ConcurrentIdenticalRequestsExecuteOnce()
{
    var repository = new FakeIdempotencyRepository();
    var userId = Guid.NewGuid();
    var actionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var finishAction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var executionCount = 0;

    var first = ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-1",
        """{"address":"Kolkata"}""",
        async response =>
        {
            Interlocked.Increment(ref executionCount);
            actionStarted.SetResult();
            await finishAction.Task;
            response.StatusCode = StatusCodes.Status200OK;
            await response.WriteAsync("""{"orderId":"first"}""");
        });

    await actionStarted.Task;
    var duplicate = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-1",
        """{"address":"Kolkata"}""",
        response =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

    AssertEqual(StatusCodes.Status409Conflict, duplicate.StatusCode,
        "A concurrent duplicate request must report that processing is in progress.");
    AssertEqual(1, executionCount,
        "Concurrent requests with the same identity and payload must execute the action once.");

    finishAction.SetResult();
    await first;
    Assert(repository.LastProcessingLease <= TimeSpan.FromMinutes(5),
        "A processing claim must use a short crash-recovery lease.");
}

static async Task IdempotencyKeyRejectsChangedPayload()
{
    var repository = new FakeIdempotencyRepository();
    var userId = Guid.NewGuid();

    await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-2",
        """{"address":"Kolkata"}""",
        async response => await response.WriteAsync("""{"orderId":"one"}"""));

    var changed = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-2",
        """{"address":"Delhi"}""",
        _ => throw new Exception("A changed request must not execute."));

    AssertEqual(StatusCodes.Status409Conflict, changed.StatusCode,
        "Reusing a key with a different payload must return a conflict.");
}

static async Task CompletedResponseReplaysExactly()
{
    var repository = new FakeIdempotencyRepository();
    var userId = Guid.NewGuid();
    var executionCount = 0;
    const string body = """{"orderId":"created-order"}""";

    var original = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin/",
        "checkout-3",
        """{"address":"Kolkata"}""",
        async response =>
        {
            executionCount++;
            response.StatusCode = StatusCodes.Status201Created;
            response.ContentType = "application/vnd.ecommerce.order+json";
            response.Headers.Location = "/api/orders/created-order";
            await response.WriteAsync(body);
        });

    var replay = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/API/CHECKOUT/BEGIN",
        "checkout-3",
        """{"address":"Kolkata"}""",
        _ => throw new Exception("A completed request must be replayed."));

    AssertEqual(1, executionCount, "A completed request must not execute again.");
    AssertEqual(original.StatusCode, replay.StatusCode, "Replay must preserve the original status code.");
    AssertEqual(original.ContentType, replay.ContentType, "Replay must preserve the original content type.");
    AssertEqual(original.Body, replay.Body, "Replay must preserve the original response body.");
    AssertEqual(original.Location, replay.Location, "Replay must preserve useful response headers.");
}

static async Task IdempotencyKeysAreScoped()
{
    var repository = new FakeIdempotencyRepository();
    var executionCount = 0;
    var firstUser = Guid.NewGuid();
    var secondUser = Guid.NewGuid();

    foreach (var request in new[]
             {
                 (firstUser, "/api/checkout/begin"),
                 (secondUser, "/api/checkout/begin"),
                 (firstUser, "/api/checkout/review")
             })
    {
        await ExecuteIdempotentRequestAsync(
            repository,
            request.Item1,
            request.Item2,
            "shared-key",
            "{}",
            async response =>
            {
                executionCount++;
                await response.WriteAsync($"{{\"execution\":{executionCount}}}");
            });
    }

    AssertEqual(3, executionCount,
        "The same caller key must remain independent across authenticated users and routes.");
}

static async Task FailedIdempotentRequestsCanRetry()
{
    var repository = new FakeIdempotencyRepository();
    var userId = Guid.NewGuid();

    await AssertThrowsAsync<InvalidOperationException>(() =>
        ExecuteIdempotentRequestAsync(
            repository,
            userId,
            "/api/checkout/begin",
            "checkout-4",
            "{}",
            _ => throw new InvalidOperationException("Simulated failure")));

    var retry = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-4",
        "{}",
        async response => await response.WriteAsync("""{"orderId":"retry"}"""));

    AssertEqual(StatusCodes.Status200OK, retry.StatusCode,
        "A failed request must release its claim so the caller can retry.");
    AssertEqual("""{"orderId":"retry"}""", retry.Body,
        "The retry must execute and return its own response.");
}

static async Task RequestRepresentationIsFingerprinted()
{
    var repository = new FakeIdempotencyRepository();
    var userId = Guid.NewGuid();

    await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-5",
        "{}",
        async response => await response.WriteAsync("""{"orderId":"one"}"""),
        "application/json");

    var changed = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-5",
        "{}",
        _ => throw new Exception("A changed representation must not execute."),
        "text/json");

    AssertEqual(StatusCodes.Status409Conflict, changed.StatusCode,
        "Reusing a key with a different content type must return a conflict.");
}

static async Task InvalidIdempotencyKeysAreRejected()
{
    var repository = new FakeIdempotencyRepository();
    var response = await ExecuteIdempotentRequestAsync(
        repository,
        Guid.NewGuid(),
        "/api/checkout/begin",
        "invalid key",
        "{}",
        _ => throw new Exception("An invalid key must not execute the action."));

    AssertEqual(StatusCodes.Status400BadRequest, response.StatusCode,
        "Keys containing unsupported characters must be rejected.");
}

static async Task ServerErrorsAreNotCached()
{
    var repository = new FakeIdempotencyRepository();
    var userId = Guid.NewGuid();
    var executionCount = 0;

    var failed = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-6",
        "{}",
        response =>
        {
            executionCount++;
            response.StatusCode = StatusCodes.Status500InternalServerError;
            return response.WriteAsync("""{"error":"temporary"}""");
        });

    var retry = await ExecuteIdempotentRequestAsync(
        repository,
        userId,
        "/api/checkout/begin",
        "checkout-6",
        "{}",
        response =>
        {
            executionCount++;
            return response.WriteAsync("""{"orderId":"retry"}""");
        });

    AssertEqual(StatusCodes.Status500InternalServerError, failed.StatusCode,
        "The original server error must be returned.");
    AssertEqual(StatusCodes.Status200OK, retry.StatusCode,
        "A server error must release the key for retry.");
    AssertEqual(2, executionCount, "A server-error response must not be replayed from cache.");
}

static async Task<(int StatusCode, string? ContentType, string Body, string? Location)>
    ExecuteIdempotentRequestAsync(
        IIdempotencyRepository repository,
        Guid userId,
        string path,
        string key,
        string requestBody,
        Func<HttpResponse, Task> action,
        string? contentType = "application/json")
{
    var responseStream = new MemoryStream();
    var httpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "Test"))
    };
    httpContext.Request.Method = HttpMethods.Post;
    httpContext.Request.Path = path;
    httpContext.Request.ContentType = contentType;
    httpContext.Request.Headers["Idempotency-Key"] = key;
    httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
    httpContext.Response.Body = responseStream;
    var requestServices = new ServiceCollection();
    requestServices.AddLogging();
    requestServices.AddMvcCore();
    httpContext.RequestServices = requestServices.BuildServiceProvider();

    var actionContext = new ActionContext(
        httpContext,
        new RouteData(),
        new ActionDescriptor(),
        new ModelStateDictionary());
    var filters = new List<IFilterMetadata>();
    var executingContext = new ResourceExecutingContext(
        actionContext,
        filters,
        []);
    var filter = new IdempotencyFilter(repository);

    await filter.OnResourceExecutionAsync(
        executingContext,
        async () =>
        {
            await action(httpContext.Response);
            return new ResourceExecutedContext(actionContext, filters);
        });

    if (executingContext.Result is not null)
        await executingContext.Result.ExecuteResultAsync(actionContext);

    responseStream.Position = 0;
    using var reader = new StreamReader(responseStream, Encoding.UTF8);
    return (
        httpContext.Response.StatusCode,
        httpContext.Response.ContentType,
        await reader.ReadToEndAsync(),
        httpContext.Response.Headers.Location.ToString());
}

static async Task InvokeTaskAsync(
    MethodInfo method,
    object? instance,
    params object?[] arguments)
{
    try
    {
        await (Task)(method.Invoke(instance, arguments)
            ?? throw new Exception($"{method.Name} did not return a task."));
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
        throw ex.InnerException;
    }
}

static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Test"))
        }
    };
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

sealed class FakeIdempotencyRepository : IIdempotencyRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IdempotencyRecord> _records = [];

    public TimeSpan LastProcessingLease { get; private set; }

    public Task<IdempotencyClaim> ClaimAsync(
        string key,
        string requestHash,
        string leaseId,
        TimeSpan processingLease)
    {
        lock (_gate)
        {
            LastProcessingLease = processingLease;
            if (_records.TryGetValue(key, out var existing))
                return Task.FromResult(new IdempotencyClaim(
                    IdempotencyClaimStatus.Existing,
                    existing));

            var processing = new IdempotencyRecord(
                IdempotencyRecordState.Processing,
                requestHash,
                leaseId,
                null);
            _records[key] = processing;
            return Task.FromResult(new IdempotencyClaim(
                IdempotencyClaimStatus.Acquired,
                processing));
        }
    }

    public Task<bool> CompleteAsync(
        string key,
        IdempotencyRecord processingRecord,
        IdempotencyResponse response,
        TimeSpan completedTtl)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(key, out var current) || current != processingRecord)
                return Task.FromResult(false);

            _records[key] = processingRecord with
            {
                State = IdempotencyRecordState.Completed,
                Response = response
            };
            return Task.FromResult(true);
        }
    }

    public Task<bool> ReleaseAsync(string key, IdempotencyRecord processingRecord)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(key, out var current) || current != processingRecord)
                return Task.FromResult(false);

            _records.Remove(key);
            return Task.FromResult(true);
        }
    }

    public Task<bool> RenewAsync(
        string key,
        IdempotencyRecord processingRecord,
        TimeSpan processingLease)
    {
        lock (_gate)
        {
            var renewed = _records.TryGetValue(key, out var current)
                && current == processingRecord;
            return Task.FromResult(renewed);
        }
    }
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

    public Task ExecuteInSerializableTransactionAsync(Func<Task> operation) => operation();

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
    public int FailCalls { get; private set; }
    public OutboxMessage? LastOutboxMessage { get; private set; }
    public int CompletedSettlements { get; private set; }

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
        FailCalls++;
        var result = FailResults.Count > 0
            ? FailResults.Dequeue()
            : OrderTransitionResult.Succeeded;
        return Task.FromResult(result);
    }

    public Task<StockSettlementResult> SettleStockAsync(Guid orderId, DateTime settledAt)
    {
        if (Order is null || Order.Id != orderId)
            return Task.FromResult(StockSettlementResult.OrderNotFound);
        if (Order.Status != OrderStatus.Confirmed)
            return Task.FromResult(StockSettlementResult.OrderNotConfirmed);
        if (Order.StockSettledAt.HasValue)
            return Task.FromResult(StockSettlementResult.AlreadySettled);

        Order.StockSettledAt = settledAt;
        CompletedSettlements++;
        return Task.FromResult(StockSettlementResult.Settled);
    }

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

    public Task<bool> ReservationExistsAsync(Guid orderId, int productId) =>
        Task.FromResult(_reservations.ContainsKey((orderId, productId)));

    public Task<bool> ConfirmAsync(Guid orderId, int productId, int quantity)
    {
        return Task.FromResult(_reservations.Remove((orderId, productId)));
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

sealed class RecordingCartService : ICartService
{
    public Guid LastUserId { get; private set; }

    public Task ApplyCartDiffAsync(Guid userId, CartDiffDTO diff)
    {
        LastUserId = userId;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId)
    {
        LastUserId = userId;
        return Task.FromResult<IEnumerable<CartItem>>([]);
    }
}

sealed class RecordingCheckoutService : ICheckoutService
{
    public Guid LastUserId { get; private set; }
    public int InvoiceGenerationCalls { get; private set; }

    public Task<BeginCheckoutResult> BeginCheckoutAsync(Guid userId, BeginCheckoutModel model)
    {
        LastUserId = userId;
        return Task.FromResult(new BeginCheckoutResult());
    }

    public Task<byte[]> GenerateInvoiceForOrderAsync(Guid orderId)
    {
        InvoiceGenerationCalls++;
        return Task.FromResult(new byte[] { 1, 2, 3 });
    }

    public Task ReleaseStockAsync(Guid orderId, Guid userId)
    {
        LastUserId = userId;
        return Task.CompletedTask;
    }

    public Task ConfirmStockAsync(Guid orderId, Guid userId)
    {
        LastUserId = userId;
        return Task.CompletedTask;
    }
}

sealed class RecordingOrderedItemService : IOrderedItemService
{
    public Guid LastUserId { get; private set; }
    public UserInvoice? Invoice { get; private set; }
    public int UploadCalls { get; private set; }
    public int FulfilledCalls { get; private set; }

    public Task<List<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId)
    {
        LastUserId = userId;
        return Task.FromResult(new List<UserInvoice>());
    }

    public Task<UserInvoice> GetOrCreateInvoiceAsync(
        Guid orderId,
        Guid userId,
        int numberOfItems,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        Invoice ??= new UserInvoice
        {
            OrderId = orderId,
            UserId = userId,
            NumberOfItems = numberOfItems,
            TotalAmount = totalAmount,
            InvoiceDate = DateTime.UtcNow,
            InvoiceLink = $"invoices/{orderId}.pdf"
        };
        return Task.FromResult(Invoice);
    }

    public Task MarkAttemptStartedAsync(
        UserInvoice invoice,
        DateTime attemptedAt,
        CancellationToken cancellationToken)
    {
        invoice.AttemptCount++;
        invoice.LastAttemptAt = attemptedAt;
        return Task.CompletedTask;
    }

    public Task UploadInvoiceAsync(
        UserInvoice invoice,
        byte[] invoiceBytes,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        UploadCalls++;
        invoice.BlobUploadedAt = uploadedAt;
        return Task.CompletedTask;
    }

    public Task MarkEmailDispatchedAsync(
        UserInvoice invoice,
        DateTime dispatchedAt,
        CancellationToken cancellationToken)
    {
        invoice.EmailDispatchedAt = dispatchedAt;
        return Task.CompletedTask;
    }

    public Task MarkFulfilledAsync(
        UserInvoice invoice,
        DateTime fulfilledAt,
        CancellationToken cancellationToken)
    {
        FulfilledCalls++;
        invoice.FulfilledAt = fulfilledAt;
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(
        UserInvoice invoice,
        string error,
        DateTime attemptedAt,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

sealed class RecordingFulfillmentPublisher : IFulfillmentPublisher
{
    public int PublishCalls { get; private set; }

    public Task PublishAsync(
        OrderFulfillmentMessage message,
        CancellationToken cancellationToken = default)
    {
        PublishCalls++;
        return Task.CompletedTask;
    }
}

sealed class RecordingEmailService : IEmailService
{
    public int SendCalls { get; private set; }

    public Task SendInvoiceEmailAsync(
        IConfiguration config,
        byte[] pdfBytes,
        string receiverEmail,
        CancellationToken cancellationToken)
    {
        SendCalls++;
        return Task.CompletedTask;
    }
}
