namespace ECommerceBackend.Domain.Entities
{
    public enum OrderStatus
    {
        Pending = 0,   // reserved in Redis, awaiting payment
        Confirmed = 1, // payment succeeded, stock settled to SQL
        Cancelled = 2, // released (user cancelled)
        Failed = 3     // payment failed / reservation expired
    }
}
