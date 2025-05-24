using Azure.Core;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Factory;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Identity.Client;
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


        public async Task<AuthResponse> AuthenticateAsync(LoginModel model, string userAgent)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
            if (existingUser == null || !VerifyPassword(existingUser, model.Password))
                throw new UnauthorizedAccessException("Invalid credentials");

            var accessToken = GenerateAccessToken(existingUser);
            var refreshToken = GenerateRefreshToken();

            // Generate Refresh Token object and save it into DB
            var refreshTokenObj = RefreshTokenFactory.Create(existingUser.UserId, refreshToken, DateTime.UtcNow.AddDays(7), userAgent);
            _tokenRepository.AddAsync(refreshTokenObj).Wait(); // Save the refresh token to the database

            return await Task.FromResult(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = existingUser.UserId
            });
        }
        public async Task<AuthResponse> RegisterAsync(RegisterModel model, string userAgent)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
            if (existingUser != null)
                throw new Exception("User already exists");

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

            // Generate Refresh Token object and save it into DB
            var refreshTokenObj = RefreshTokenFactory.Create(user.UserId, refreshToken, DateTime.UtcNow.AddDays(7), userAgent);
            _tokenRepository.AddAsync(refreshTokenObj).Wait(); // Save the refresh token to the database

            
            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.UserId
            };
        }
        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string userAgent)
        {
            // Validate the refresh token and generate a new access token
            Guid? userId = await _tokenRepository.GetUserIdByTokenAsync(refreshToken);
            if (!userId.HasValue) // Check if the nullable Guid has a value
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            // Use userId.Value to safely access the non-nullable Guid
            User? user = await _userRepository.GetUserByUserIdAsync(userId.Value); // Retrieve the user using the non-nullable Guid
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Generate Refresh Token object and save it into DB
            var refreshTokenObj = RefreshTokenFactory.Create(userId.Value, refreshToken, DateTime.UtcNow.AddDays(7), userAgent);
            _tokenRepository.AddAsync(refreshTokenObj).Wait(); // Save the refresh token to the database
            Console.WriteLine("New Refresh token generated");
            return new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                UserId = user.UserId
            };
        }

    }
}
