using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBackend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ICorsPolicyProvider _corsPolicyProvider;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ICorsPolicyProvider corsPolicyProvider, ILogger<AuthController> logger)
        {
            _authService = authService;
            _corsPolicyProvider = corsPolicyProvider;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model, CancellationToken cancellationToken = default)
        {
            await RequireTrustedOriginAsync();
            var authResponse = await _authService.AuthenticateAsync(
                model, Request.Headers.UserAgent, cancellationToken);
            SetRefreshTokenInCookie(authResponse.RefreshToken);
            return Ok(new { authResponse.AccessToken, authResponse.UserId });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model, CancellationToken cancellationToken = default)
        {
            await RequireTrustedOriginAsync();
            var authResponse = await _authService.RegisterAsync(
                model, Request.Headers.UserAgent, cancellationToken);
            SetRefreshTokenInCookie(authResponse.RefreshToken);
            return Ok(new { authResponse.AccessToken, authResponse.UserId });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            await RequireTrustedOriginAsync();
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                    throw new UnauthorizedAccessException("Invalid or expired refresh token");

                var authResponse = await _authService.RefreshTokenAsync(
                    refreshToken, Request.Headers.UserAgent, cancellationToken);
                SetRefreshTokenInCookie(authResponse.RefreshToken);
                return Ok(new { authResponse.AccessToken, authResponse.UserId });
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(exception, "Refresh token rejected for {Path}", Request.Path);
                ClearRefreshTokenCookie();
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: exception.Message,
                    instance: Request.Path);
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
        {
            await RequireTrustedOriginAsync();
            await _authService.LogoutAsync(Request.Cookies["refreshToken"], cancellationToken);
            ClearRefreshTokenCookie();
            return NoContent();
        }

        private async Task RequireTrustedOriginAsync()
        {
            Response.Headers.CacheControl = "no-store";
            // SameSite=None preserves cross-site clients; validate Origin rather than relying on CORS alone.
            var origins = Request.Headers.Origin;
            if (origins.Count == 1 && !string.IsNullOrEmpty(origins[0]) && origins[0] != "null")
            {
                var origin = origins[0]!;
                if (string.Equals(origin, $"{Request.Scheme}://{Request.Host}", StringComparison.OrdinalIgnoreCase))
                    return;

                var policy = await _corsPolicyProvider.GetPolicyAsync(HttpContext, "AllowFrontend");
                if (policy is { AllowAnyOrigin: false } && policy.IsOriginAllowed(origin))
                    return;
            }

            throw new ForbiddenAccessException("A trusted Origin header is required.");
        }

        private static CookieOptions RefreshCookieOptions() => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        };

        private void SetRefreshTokenInCookie(string refreshToken)
        {
            var options = RefreshCookieOptions();
            options.Expires = DateTimeOffset.UtcNow.AddDays(7);
            Response.Cookies.Append("refreshToken", refreshToken, options);
        }

        private void ClearRefreshTokenCookie() =>
            Response.Cookies.Delete("refreshToken", RefreshCookieOptions());
    }
}
