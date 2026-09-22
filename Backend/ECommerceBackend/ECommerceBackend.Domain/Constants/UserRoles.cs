namespace ECommerceBackend.Domain.Constants
{
    // Canonical role names. Stored on the User row and emitted as a role claim in the
    // access token, so authorization ([Authorize(Roles = ...)] / User.IsInRole) is driven
    // by persisted data rather than an unbacked claim.
    public static class UserRoles
    {
        public const string Customer = "Customer";
        public const string Admin = "Admin";
    }
}
