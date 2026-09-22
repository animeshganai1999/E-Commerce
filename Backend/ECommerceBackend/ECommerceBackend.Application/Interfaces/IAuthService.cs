using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Models;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> AuthenticateAsync(LoginModel request, string? userAgent, CancellationToken cancellationToken = default);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? userAgent, CancellationToken cancellationToken = default);
        Task<AuthResponse> RegisterAsync(RegisterModel model, string? userAgent, CancellationToken cancellationToken = default);
        Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
    }
}
