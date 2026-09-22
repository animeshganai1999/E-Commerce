using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Factory;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
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
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _secret;
        private readonly int _expiryMinutes;

        public AuthService(IUserRepository userRepository, ITokenRepository tokenRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
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
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString())
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

        private static bool VerifyPassword(User user, string Password)
        {
            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Password);

            if (result == PasswordVerificationResult.Failed)
                return false;

            return true;
        }


        public async Task<AuthResponse> AuthenticateAsync(LoginModel model, string? userAgent, CancellationToken cancellationToken = default)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
            if (existingUser == null || !VerifyPassword(existingUser, model.Password))
                throw new UnauthorizedAccessException("Invalid credentials");

            var accessToken = GenerateAccessToken(existingUser);
            var refreshToken = GenerateRefreshToken();

            var refreshTokenObj = RefreshTokenFactory.Create(existingUser.UserId, refreshToken, DateTime.UtcNow.AddDays(7), userAgent);
            await _tokenRepository.AddAsync(refreshTokenObj, cancellationToken);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = existingUser.UserId
            };
        }
        public async Task<AuthResponse> RegisterAsync(RegisterModel model, string? userAgent, CancellationToken cancellationToken = default)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
            if (existingUser != null)
                throw new ArgumentException("User already exists");

            // Ensure all required properties of the User object are set
            var user = new User
            {
                UserId = Guid.NewGuid(), // Generate a new GUID for the UserId
                Name = model.Name,
                Email = model.Email,
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
