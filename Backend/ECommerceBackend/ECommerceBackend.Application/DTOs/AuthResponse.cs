namespace ECommerceBackend.Application.DTOs
{
    public class AuthResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required Guid UserId { get; set; }
    }
}
