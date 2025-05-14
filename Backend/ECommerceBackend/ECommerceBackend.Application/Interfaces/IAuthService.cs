using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity.Data;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> AuthenticateAsync(LoginModel request, string userAgent);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? userAgent);
        Task<AuthResponse> RegisterAsync(RegisterModel model, string userAgent);
    }
}
