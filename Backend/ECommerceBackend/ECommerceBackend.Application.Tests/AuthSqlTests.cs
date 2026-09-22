using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ECommerceBackend.API.Controllers;
using ECommerceBackend.API.Infrastructure;
using ECommerceBackend.Application.Factory;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Services;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Data;
using ECommerceBackend.Infrastructure.Migrations;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static AuthTests;

internal static class AuthSqlTests
{
    public static async Task RunAsync()
    {
        var connection = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("ECOMMERCE_TEST_SQL_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true")
        {
            InitialCatalog = $"ECommerceAuthTests_{Guid.NewGuid():N}",
            Pooling = false
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.EnableRetryOnFailure()).Options;

        await using var database = new AppDbContext(options);
        try
        {
            await VerifyMigrationAsync(database);
            await VerifyRotationAsync(options);
            await VerifyConcurrentOperationsAsync(options);
            await VerifyHttpLifecycleAsync(options);
            await ApplyMigrationOperationsAsync(database, new RotateHashedRefreshTokens().DownOperations);
            Check(await database.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [RefreshTokens]").SingleAsync() == 0,
                "Downgrade must discard hashes rather than turn them into plaintext credentials.");
            await ApplyMigrationOperationsAsync(database, new RotateHashedRefreshTokens().UpOperations);
        }
        finally
        {
            // The database name is generated above, never taken from the supplied connection string.
            await database.Database.EnsureDeletedAsync();
        }
    }

    private static async Task VerifyMigrationAsync(AppDbContext database)
    {
        // Isolate this migration: older migrations cannot bootstrap Products on an empty database.
        await database.Database.EnsureCreatedAsync();
        var migration = new RotateHashedRefreshTokens();
        await ApplyMigrationOperationsAsync(database, migration.DownOperations);
        var userId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var raw = RawToken();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Users] ([UserId], [Name], [Email], [PasswordHash])
            VALUES ({userId}, 'Migration test', 'migration@example.invalid', 'unused');
            INSERT INTO [RefreshTokens] ([Id], [UserId], [Token], [ExpiryDate], [IsRevoked], [CreatedAt])
            VALUES ({id1}, {userId}, {raw}, DATEADD(day, 7, SYSUTCDATETIME()), 0, SYSUTCDATETIME()),
                   ({id2}, {userId}, {raw}, DATEADD(day, 7, SYSUTCDATETIME()), 0, SYSUTCDATETIME());
            """);
        await ApplyMigrationOperationsAsync(database, migration.UpOperations);
        Check(!await database.RefreshTokens.AnyAsync(), "Migration must invalidate all legacy plaintext tokens, including duplicates.");
        Check(await database.Users.AnyAsync(u => u.UserId == userId), "Migration must preserve users.");
        Check(!database.Database.HasPendingModelChanges(), "Migration snapshot must match the model.");
    }

    private static async Task ApplyMigrationOperationsAsync(AppDbContext database,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> operations)
    {
        var commands = database.GetService<IMigrationsSqlGenerator>().Generate(operations);
        await database.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await database.Database.BeginTransactionAsync();
            foreach (var command in commands)
                await database.Database.ExecuteSqlRawAsync(command.CommandText);
            await transaction.CommitAsync();
        });
    }

    private static async Task VerifyRotationAsync(DbContextOptions<AppDbContext> options)
    {
        var userId = await CreateUserAsync(options);
        var initial = await IssueAsync(options, userId);
        var otherSession = await IssueAsync(options, userId);
        var replacement = RawToken();
        Check(await RotateAsync(options, initial.Raw, replacement) == userId, "First rotation must succeed.");
        var newest = RawToken();
        Check(await RotateAsync(options, replacement, newest) == userId, "The replacement must work.");

        await using (var context = new AppDbContext(options))
        {
            var tokens = await context.RefreshTokens.AsNoTracking()
                .Where(t => t.FamilyId == initial.Id).ToListAsync();
            Check(tokens.Count == 3, "Each rotation must create exactly one replacement.");
            var root = tokens.Single(t => t.Id == initial.Id);
            Check(root.IsRevoked && root.RevokedAt.HasValue
                && root.ReplacedByTokenHash == RefreshTokenFactory.Hash(replacement), "Rotation must record revocation and replacement hash.");
            Check(tokens.Count(t => !t.IsRevoked) == 1, "Only the newest token may be active.");
            await context.RefreshTokens.Where(t => t.Id == initial.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiryDate, DateTime.UtcNow.AddDays(-1)));
        }

        Check(await RotateAsync(options, initial.Raw, RawToken()) is null, "Replay of an expired ancestor must be rejected.");
        await AssertFamilyRevokedAsync(options, initial.Id);
        Check(await RotateAsync(options, newest, RawToken()) is null, "Replay must revoke the latest descendant.");
        Check(await RotateAsync(options, otherSession.Raw, RawToken()) == userId, "Replay must not revoke unrelated sessions.");

        var expired = await IssueAsync(options, userId, expired: true);
        var revoked = await IssueAsync(options, userId, revoked: true);
        Check(await RotateAsync(options, expired.Raw, RawToken()) is null, "Expired tokens must be rejected.");
        Check(await RotateAsync(options, revoked.Raw, RawToken()) is null, "Revoked tokens must be rejected.");
        Check(await RotateAsync(options, RawToken(), RawToken()) is null, "Unknown tokens must be rejected.");

        var rollback = await IssueAsync(options, userId);
        await ThrowsAsync<DbUpdateException>(() => RotateAsync(options, rollback.Raw, expired.Raw));
        await using (var context = new AppDbContext(options))
        {
            var root = await context.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == rollback.Id);
            Check(!root.IsRevoked && root.RevokedAt is null && root.ReplacedByTokenHash is null,
                "An insert failure must roll back revocation and the replacement pointer.");
        }
        Check(await RotateAsync(options, rollback.Raw, RawToken()) == userId, "A rolled-back token must still be usable.");
    }

    private static async Task VerifyConcurrentOperationsAsync(DbContextOptions<AppDbContext> options)
    {
        var userId = await CreateUserAsync(options);
        var initial = await IssueAsync(options, userId);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            await gate.Task;
            return await RotateAsync(options, initial.Raw, RawToken());
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(attempts);
        Check(results.Count(id => id.HasValue) == 1, "Concurrent requests must produce exactly one successful rotation.");
        await using (var context = new AppDbContext(options))
        {
            Check(await context.RefreshTokens.CountAsync(t => t.FamilyId == initial.Id) == 2,
                "Concurrent requests must insert only one replacement.");
        }
        await AssertFamilyRevokedAsync(options, initial.Id);

        // Both races must leave no live descendant, irrespective of which transaction takes the root lock first.
        foreach (var replay in new[] { false, true })
        {
            var root = await IssueAsync(options, userId);
            var child = RawToken();
            Check(await RotateAsync(options, root.Raw, child) == userId, "Prepare a child token.");
            var raceGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task RotateChild()
            {
                await raceGate.Task;
                await RotateAsync(options, child, RawToken());
            }
            async Task InvalidateFamily()
            {
                await raceGate.Task;
                if (replay)
                    await RotateAsync(options, root.Raw, RawToken());
                else
                {
                    await using var context = new AppDbContext(options);
                    await Repository(context).RevokeFamilyAsync(RefreshTokenFactory.Hash(root.Raw));
                }
            }
            var tasks = new[] { RotateChild(), InvalidateFamily() };
            raceGate.SetResult();
            await Task.WhenAll(tasks);
            await AssertFamilyRevokedAsync(options, root.Id);
        }
    }

    private static async Task VerifyHttpLifecycleAsync(DbContextOptions<AppDbContext> options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Configuration.AddConfiguration(Configuration());
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<AppDbContext>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ITokenRepository, TokenRepository>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddCors(cors => cors.AddPolicy("AllowFrontend",
            policy => policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly)
            .AddJsonOptions(json => json.JsonSerializerOptions.PropertyNamingPolicy = null);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseCors("AllowFrontend");
        app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                BaseAddress = new Uri(app.Urls.Single())
            };
            var loginBody = new { Email = $"http-{Guid.NewGuid():N}@example.invalid", Password = "Test password only!" };
            using var registration = await SendAsync(client, "register", body: new
            {
                Name = "HTTP test", loginBody.Email, loginBody.Password
            });
            var first = await ReadSessionCookieAsync(registration);
            using var login = await SendAsync(client, "login", body: loginBody);
            var independent = await ReadSessionCookieAsync(login);
            using var badLogin = await SendAsync(client, "login", body: new { loginBody.Email, Password = "wrong" });
            Check(badLogin.StatusCode == HttpStatusCode.Unauthorized, "Invalid credentials must return 401, not 400.");
            using var missing = await SendAsync(client, "refresh-token");
            Check(missing.StatusCode == HttpStatusCode.Unauthorized, "A missing refresh cookie must return 401.");

            foreach (var origin in new string?[] { null, "null", "https://untrusted.example" })
            {
                using var blocked = await SendAsync(client, "refresh-token", first, origin: origin);
                Check(blocked.StatusCode == HttpStatusCode.Forbidden, "Missing/untrusted origins must return 403.");
            }

            using var refreshed = await SendAsync(client, "refresh-token", first);
            var second = await ReadSessionCookieAsync(refreshed);
            Check(second != first, "HTTP refresh must replace the cookie.");
            using var replay = await SendAsync(client, "refresh-token", first);
            Check(replay.StatusCode == HttpStatusCode.Unauthorized, "HTTP replay must return 401.");
            Check(replay.Headers.GetValues("Set-Cookie").Any(value => value.Contains("expires=")), "Replay must clear the cookie.");
            using var descendant = await SendAsync(client, "refresh-token", second);
            Check(descendant.StatusCode == HttpStatusCode.Unauthorized, "Replayed session descendants must fail.");
            using var independentRefresh = await SendAsync(client, "refresh-token", independent);
            var active = await ReadSessionCookieAsync(independentRefresh);
            using var logout = await SendAsync(client, "logout", active);
            Check(logout.StatusCode == HttpStatusCode.NoContent, "Logout must return 204.");
            Check(logout.Headers.GetValues("Set-Cookie").Any(value =>
                value.Contains("expires=") && value.Contains("path=/")), "Logout must expire the matching cookie.");
            using var afterLogout = await SendAsync(client, "refresh-token", active);
            Check(afterLogout.StatusCode == HttpStatusCode.Unauthorized, "Logout must revoke the session in SQL.");
            using var repeatedLogout = await SendAsync(client, "logout", active);
            using var anonymousLogout = await SendAsync(client, "logout");
            Check(repeatedLogout.StatusCode == HttpStatusCode.NoContent
                && anonymousLogout.StatusCode == HttpStatusCode.NoContent, "Logout must be idempotent.");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<string> ReadSessionCookieAsync(HttpResponseMessage response)
    {
        Check(response.StatusCode == HttpStatusCode.OK, $"Expected authentication success, received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Check(json.RootElement.TryGetProperty("AccessToken", out _) && json.RootElement.TryGetProperty("UserId", out _)
            && !json.RootElement.TryGetProperty("RefreshToken", out _), "Preserve the public response shape without exposing the refresh token.");
        Check(response.Headers.CacheControl?.NoStore == true, "Authentication responses must be non-cacheable.");
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("refreshToken="));
        Check(cookie.Contains("secure") && cookie.Contains("httponly") && cookie.Contains("samesite=none"),
            "HTTP cookies must retain their security attributes.");
        return cookie.Split(';')[0];
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string endpoint,
        string? cookie = null, object? body = null, string? origin = "http://localhost:3000")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{endpoint}");
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        if (cookie is not null)
            request.Headers.Add("Cookie", cookie);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return SendAndDisposeAsync(client, request);
    }

    private static async Task<HttpResponseMessage> SendAndDisposeAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request)
            return await client.SendAsync(request);
    }

    private static async Task<Guid> CreateUserAsync(DbContextOptions<AppDbContext> options)
    {
        await using var context = new AppDbContext(options);
        var user = new User
        {
            UserId = Guid.NewGuid(), Name = "SQL test",
            Email = $"{Guid.NewGuid():N}@example.invalid", PasswordHash = "unused"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.UserId;
    }

    private static async Task<(Guid Id, string Raw)> IssueAsync(DbContextOptions<AppDbContext> options,
        Guid userId, bool expired = false, bool revoked = false)
    {
        await using var context = new AppDbContext(options);
        var raw = RawToken();
        var token = RefreshTokenFactory.Create(userId, raw, DateTime.UtcNow.AddDays(expired ? -1 : 7));
        token.IsRevoked = revoked;
        token.RevokedAt = revoked ? DateTime.UtcNow : null;
        await Repository(context).AddAsync(token);
        return (token.Id, raw);
    }

    private static async Task<Guid?> RotateAsync(DbContextOptions<AppDbContext> options, string oldToken, string newToken)
    {
        await using var context = new AppDbContext(options);
        return await Repository(context).RotateAsync(
            RefreshTokenFactory.Hash(oldToken), RefreshTokenFactory.Hash(newToken),
            DateTime.UtcNow.AddDays(7), "sql-test");
    }

    private static async Task AssertFamilyRevokedAsync(DbContextOptions<AppDbContext> options, Guid familyId)
    {
        await using var context = new AppDbContext(options);
        Check(!await context.RefreshTokens.AnyAsync(t => t.FamilyId == familyId && !t.IsRevoked),
            "The invalidated family must have no live descendants.");
    }

    private static string RawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static TokenRepository Repository(AppDbContext context) =>
        new(context, NullLogger<TokenRepository>.Instance);
}
