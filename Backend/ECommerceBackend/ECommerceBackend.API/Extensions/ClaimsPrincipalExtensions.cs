using System.Security.Claims;

namespace ECommerceBackend.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetRequiredUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(value, out var userId))
                throw new UnauthorizedAccessException("The access token does not contain a valid user identifier.");

            return userId;
        }
    }
}
