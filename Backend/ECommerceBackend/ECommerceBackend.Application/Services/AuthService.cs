using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Factory;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceBackend.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly ILogger<AuthService> _logger;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _secret;
        private readonly int _expiryMinutes;

        // A shared instance and precomputed hash used only to equalize work when an account is
        // missing, so response timing does not reveal whether an email is registered.
        private static readonly User _timingUser = new()
        {
            Name = "timing",
            Email = "timing@invalid",
            PasswordHash = string.Empty
        };
        private static readonly string _timingHash =
            new PasswordHasher<User>().HashPassword(_timingUser, "timing-equalization-only");

        public AuthService(
            IUserRepository userRepository,
            ITokenRepository tokenRepository,
            IConfiguration configuration,
            ILogger<AuthService>? logger = null)
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _logger = logger ?? NullLogger<AuthService>.Instance;
            _issuer = configuration["Jwt:Issuer"]!;
            _audience = configuration["Jwt:Audience"]!;
            _secret = configuration["Jwt:Secret"]!;
            _expiryMinutes = int.Parse(configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        }

        private string GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private static bool VerifyPassword(User user, string passwordHash, string password)
        {
            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, passwordHash, password);

            if (result == PasswordVerificationResult.Failed)
                return false;

            return true;
        }


        public async Task<AuthResponse> AuthenticateAsync(LoginModel model, string? userAgent, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = User.NormalizeEmail(model.Email);
            var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);

            if (existingUser is null)
            {
                // Perform an equivalent hash verification against a dummy hash so a missing
                // account is indistinguishable (by timing) from a wrong password.
                VerifyPassword(_timingUser, _timingHash, model.Password);
                _logger.LogWarning("Login failed for {NormalizedEmail}: no matching account.", normalizedEmail);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            if (!VerifyPassword(existingUser, existingUser.PasswordHash, model.Password))
            {
                _logger.LogWarning("Login failed for user {UserId}: invalid password.", existingUser.UserId);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            var accessToken = GenerateAccessToken(existingUser);
            var refreshToken = GenerateRefreshToken();

            var refreshTokenObj = RefreshTokenFactory.Create(existingUser.UserId, refreshToken, DateTime.UtcNow.AddDays(7), userAgent);
            await _tokenRepository.AddAsync(refreshTokenObj, cancellationToken);

            _logger.LogInformation("Login succeeded for user {UserId}.", existingUser.UserId);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = existingUser.UserId
            };
        }
        public async Task<AuthResponse> RegisterAsync(RegisterModel model, string? userAgent, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = User.NormalizeEmail(model.Email);
            var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
            if (existingUser != null)
            {
                // Do not confirm that the email is taken. Equalize timing with the create path
                // (which hashes a password) and return a generic error instead.
                new PasswordHasher<User>().HashPassword(_timingUser, model.Password);
                _logger.LogWarning("Registration blocked for {NormalizedEmail}: email already registered.", normalizedEmail);
                throw new ArgumentException("Registration could not be completed. Please check your details and try again.");
            }

            // Ensure all required properties of the User object are set
            var user = new User
            {
                UserId = Guid.NewGuid(), // Generate a new GUID for the UserId
                Name = model.Name,
                Email = model.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = string.Empty // Initialize PasswordHash to satisfy the required property
            };

            // Now hash the password using the user object
            var passwordHasher = new PasswordHasher<User>();
            user.PasswordHash = passwordHasher.HashPassword(user, model.Password);

            await _userRepository.AddAsync(user);

            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            var refreshTokenObj = RefreshTokenFactory.Create(user.UserId, refreshToken, DateTime.UtcNow.AddDays(7), userAgent);
            await _tokenRepository.AddAsync(refreshTokenObj, cancellationToken);

            _logger.LogInformation("Registration succeeded for user {UserId}.", user.UserId);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.UserId
            };
        }
        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? userAgent, CancellationToken cancellationToken = default)
        {
            if (!IsValidRefreshTokenFormat(refreshToken))
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            var newRefreshToken = GenerateRefreshToken();
            var userId = await _tokenRepository.RotateAsync(
                RefreshTokenFactory.Hash(refreshToken),
                RefreshTokenFactory.Hash(newRefreshToken),
                DateTime.UtcNow.AddDays(7),
                userAgent,
                cancellationToken);

            if (!userId.HasValue)
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            var user = await _userRepository.GetUserByUserIdAsync(userId.Value);
            if (user is null)
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            return new AuthResponse
            {
                AccessToken = GenerateAccessToken(user),
                RefreshToken = newRefreshToken,
                UserId = user.UserId
            };
        }

        public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            // Logout is idempotent, including when a browser no longer has a session cookie.
            if (IsValidRefreshTokenFormat(refreshToken))
                await _tokenRepository.RevokeFamilyAsync(RefreshTokenFactory.Hash(refreshToken!), cancellationToken);
        }

        private static bool IsValidRefreshTokenFormat(string? token)
        {
            if (token is null || token.Length != 88)
                return false;

            Span<byte> bytes = stackalloc byte[64];
            return Convert.TryFromBase64String(token, bytes, out var written) && written == bytes.Length;
        }
    }
}
