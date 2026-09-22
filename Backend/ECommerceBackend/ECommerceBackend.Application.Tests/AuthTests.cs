using System.Linq.Expressions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECommerceBackend.API.Controllers;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Factory;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Application.Services;
using ECommerceBackend.Domain.Constants;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

internal static class AuthTests
{
    public static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "auth-tests",
            ["Jwt:Audience"] = "auth-tests",
            ["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Jwt:AccessTokenExpiryMinutes"] = "15"
        }).Build();

    public static async Task IssuanceStoresHashesAsync()
    {
        var users = new StubUserRepository();
        var tokens = new StubTokenRepository();
        var service = new AuthService(users, tokens, Configuration());
        var registered = await service.RegisterAsync(new RegisterModel
        {
            Email = "auth-test@example.invalid",
            Name = "Auth test",
            Password = "Test password only!"
        }, "test-agent");
        CheckStoredToken(registered, tokens.LastAdded!);
        var firstFamily = tokens.LastAdded!.FamilyId;
        var login = await service.AuthenticateAsync(new LoginModel
        {
            Email = "auth-test@example.invalid",
            Password = "Test password only!"
        }, null);
        CheckStoredToken(login, tokens.LastAdded!);
        Check(firstFamily != tokens.LastAdded!.FamilyId, "Separate logins must have separate families.");
        Check(login.RefreshToken != registered.RefreshToken, "Issued tokens must be random.");
        await ThrowsAsync<UnauthorizedAccessException>(() => service.AuthenticateAsync(
            new LoginModel { Email = users.User!.Email, Password = "wrong" }, null));
    }

    public static async Task RotationContractAsync()
    {
        var users = new StubUserRepository
        {
            User = new User
            {
                UserId = Guid.NewGuid(),
                Name = "Test",
                Email = "rotation@example.invalid",
                PasswordHash = "unused"
            }
        };
        var tokens = new StubTokenRepository { RotationUserId = users.User.UserId };
        var service = new AuthService(users, tokens, Configuration());
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var rotated = await service.RefreshTokenAsync(raw, "test-agent");
        Check(tokens.OldHash == RefreshTokenFactory.Hash(raw), "Lookup must use a hash.");
        Check(tokens.NewHash == RefreshTokenFactory.Hash(rotated.RefreshToken), "Persist the replacement, not the old token.");
        Check(tokens.NewHash != tokens.OldHash, "Rotation must issue a new token.");
        Check(rotated.UserId == users.User.UserId, "The persisted token determines the user.");
        Check(tokens.LastAdded is null, "Rotation must use the atomic repository operation.");

        tokens.RotationUserId = null;
        await ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshTokenAsync(raw, null));
        var calls = tokens.RotationCalls;
        foreach (var invalid in new[] { "", "not-a-token", new string('!', 88), new string('a', 1000) })
            await ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshTokenAsync(invalid, null));
        Check(tokens.RotationCalls == calls, "Malformed tokens must not query SQL.");
        await service.LogoutAsync(raw);
        Check(tokens.RevokedHash == RefreshTokenFactory.Hash(raw), "Logout must revoke by hash.");
        await service.LogoutAsync(null);
    }

    public static async Task CookieContractAsync()
    {
        var service = new StubAuthService();
        foreach (var origin in new string?[] { null, "null", "https://untrusted.example", "http://localhost:3000.evil" })
        {
            var controller = Controller(service, origin);
            await ThrowsAsync<ForbiddenAccessException>(() => controller.RefreshTokenAsync());
            await ThrowsAsync<ForbiddenAccessException>(() => controller.Logout());
            await ThrowsAsync<ForbiddenAccessException>(() => controller.Login(
                new LoginModel { Email = "test@example.invalid", Password = "unused" }));
            await ThrowsAsync<ForbiddenAccessException>(() => controller.Register(
                new RegisterModel { Name = "Test", Email = "test@example.invalid", Password = "unused" }));
        }
        Check(service.Calls == 0, "Untrusted origins must fail before any session operation.");
        var missing = Controller(service, "http://localhost:3000");
        Check(await missing.RefreshTokenAsync() is ObjectResult { StatusCode: 401 }, "A missing token must return 401.");
        Check(missing.Response.Headers.SetCookie.ToString().Contains("expires="), "Invalid cookies must be cleared.");

        var valid = Controller(service, "http://localhost:3000");
        valid.Request.Headers.Cookie = "refreshToken=test-cookie";
        var response = await valid.RefreshTokenAsync();
        Check(response is OkObjectResult, "The existing refresh response must remain successful.");
        var cookie = valid.Response.Headers.SetCookie.ToString();
        Check(cookie.Contains("httponly") && cookie.Contains("secure")
            && cookie.Contains("samesite=none") && cookie.Contains("path=/"), "Preserve cookie security and cross-site compatibility.");
        Check(valid.Response.Headers.CacheControl == "no-store", "Token responses must not be cached.");

        var logout = Controller(service, "https://localhost:7244");
        Check(await logout.Logout() is NoContentResult, "Logout without a cookie is idempotent.");
        Check(logout.Response.Headers.SetCookie.ToString().Contains("expires="), "Logout must clear the cookie.");
    }

    public static async Task AccessTokenCarriesRoleAsync()
    {
        var config = Configuration();

        // Registration must issue a Customer-scoped access token and persist that role.
        var users = new StubUserRepository();
        var service = new AuthService(users, new StubTokenRepository(), config);
        var registered = await service.RegisterAsync(new RegisterModel
        {
            Email = "role-customer@example.invalid",
            Name = "Role customer",
            Password = "Test password only!"
        }, "test-agent");
        Check(users.User!.Role == UserRoles.Customer, "A registered user must be persisted as a Customer.");
        Check(Principal(registered.AccessToken, config).IsInRole(UserRoles.Customer),
            "A new registration's token must authorize as Customer.");
        Check(!Principal(registered.AccessToken, config).IsInRole(UserRoles.Admin),
            "A Customer token must never satisfy an Admin requirement.");

        // Login must reflect the persisted role, so a promoted Admin gets an Admin token.
        var admin = new User
        {
            UserId = Guid.NewGuid(),
            Name = "Admin",
            Email = "role-admin@example.invalid",
            PasswordHash = string.Empty,
            Role = UserRoles.Admin
        };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, "Admin password only!");
        var adminService = new AuthService(new StubUserRepository { User = admin }, new StubTokenRepository(), config);
        var login = await adminService.AuthenticateAsync(new LoginModel
        {
            Email = admin.Email,
            Password = "Admin password only!"
        }, null);
        Check(Principal(login.AccessToken, config).IsInRole(UserRoles.Admin),
            "The access token must carry the persisted Admin role.");
    }

    // Validate a token exactly as the API does, so IsInRole mirrors [Authorize(Roles = ...)].
    private static ClaimsPrincipal Principal(string accessToken, IConfiguration config)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!))
        };
        return new JwtSecurityTokenHandler().ValidateToken(accessToken, parameters, out _);
    }

    private static AuthController Controller(IAuthService service, string? origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 7244);
        if (origin is not null)
            context.Request.Headers.Origin = origin;
        return new AuthController(service, new StubCorsPolicyProvider(), NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static void CheckStoredToken(AuthResponse response, RefreshToken stored)
    {
        Check(stored.TokenHash == RefreshTokenFactory.Hash(response.RefreshToken), "Only the token hash belongs in SQL.");
        Check(stored.TokenHash.Length == 64 && stored.TokenHash != response.RefreshToken, "Persist a SHA-256 hash, never plaintext.");
        Check(stored.Id != Guid.Empty && stored.FamilyId == stored.Id, "A new session needs a durable root.");
        Check(!stored.IsRevoked && stored.RevokedAt is null && stored.ReplacedByTokenHash is null, "New tokens must be active.");
        Check(stored.ExpiryDate > DateTime.UtcNow.AddDays(6), "New tokens must retain the seven-day lifetime.");
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class StubTokenRepository : ITokenRepository
    {
        public RefreshToken? LastAdded { get; private set; }
        public Guid? RotationUserId { get; set; }
        public string? OldHash { get; private set; }
        public string? NewHash { get; private set; }
        public string? RevokedHash { get; private set; }
        public int RotationCalls { get; private set; }

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
        {
            LastAdded = token;
            return Task.CompletedTask;
        }

        public Task<Guid?> RotateAsync(string tokenHash, string replacementHash, DateTime replacementExpiry,
            string? userAgent, CancellationToken cancellationToken = default)
        {
            RotationCalls++;
            OldHash = tokenHash;
            NewHash = replacementHash;
            return Task.FromResult(RotationUserId);
        }

        public Task RevokeFamilyAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            RevokedHash = tokenHash;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUserRepository : IUserRepository
    {
        public User? User { get; set; }
        public Task<User?> GetUserByEmailAsync(string email) =>
            Task.FromResult(User?.Email == email ? User : null);
        public Task<User?> GetUserByUserIdAsync(Guid? userId) =>
            Task.FromResult(User?.UserId == userId ? User : null);
        public Task AddAsync(User user) { User = user; return Task.CompletedTask; }
        public Task<User> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<User>> GetAllAsync() => throw new NotSupportedException();
        public Task UpdateAsync(User user) => throw new NotSupportedException();
        public Task DeleteAsync(Expression<Func<User, bool>> filter) => throw new NotSupportedException();
    }

    private sealed class StubCorsPolicyProvider : ICorsPolicyProvider
    {
        public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName) =>
            Task.FromResult<CorsPolicy?>(new CorsPolicyBuilder().WithOrigins("http://localhost:3000").Build());
    }

    private sealed class StubAuthService : IAuthService
    {
        public int Calls { get; private set; }
        private Task<AuthResponse> Respond()
        {
            Calls++;
            return Task.FromResult(new AuthResponse
            {
                AccessToken = "test-access",
                RefreshToken = "test-refresh",
                UserId = Guid.NewGuid()
            });
        }
        public Task<AuthResponse> AuthenticateAsync(LoginModel request, string? userAgent, CancellationToken cancellationToken = default) => Respond();
        public Task<AuthResponse> RegisterAsync(RegisterModel model, string? userAgent, CancellationToken cancellationToken = default) => Respond();
        public Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? userAgent, CancellationToken cancellationToken = default) => Respond();
        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
