using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Application.Services;
using ECommerceBackend.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceBackend.API.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            string? userAgent = HttpContext.Request.Headers.UserAgent;
            try
            {
                var authResponse = await _authService.AuthenticateAsync(model, userAgent);
                SetRefreshTokenInCookie(authResponse.RefreshToken);
                //Console.WriteLine($"RefreshToken: {authResponse.RefreshToken}");
                return Ok(new
                {
                    authResponse.AccessToken,
                    authResponse.UserId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            string? userAgent = HttpContext.Request.Headers.UserAgent;
            try
            {
                var authResponse = await _authService.RegisterAsync(model, userAgent);
                SetRefreshTokenInCookie(authResponse.RefreshToken);

                return Ok(new
                {
                    authResponse.AccessToken,
                    authResponse.UserId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshTokenAsync()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            string? userAgent = HttpContext.Request.Headers.UserAgent;

            if (string.IsNullOrEmpty(refreshToken))
            {
                return BadRequest(new { message = "No refresh token provided" });
            }
            try
            {
                var authResponse = await _authService.RefreshTokenAsync(refreshToken, userAgent);
                SetRefreshTokenInCookie(authResponse.RefreshToken);

                return Ok(new
                {
                    authResponse.AccessToken,
                    authResponse.UserId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private void SetRefreshTokenInCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Only send on HTTPS
                SameSite = SameSiteMode.None, //This allows sending cookies in cross-origin requests
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
    }
}
