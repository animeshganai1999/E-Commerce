namespace ECommerceBackend.Application.Constants
{
    public static class StockMaintenanceLock
    {
        public const string Key = "lock:stock-maintenance";
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    }
}
