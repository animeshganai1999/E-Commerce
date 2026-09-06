namespace ECommerceBackend.Domain.Entities
{
    public enum OrderTransitionResult
    {
        Succeeded,
        AlreadyConfirmed,
        AlreadyFailed,
        Expired,
        NotFound,
        Forbidden
    }
}
