namespace ECommerceBackend.Domain.Entities
{
    public enum StockSettlementResult
    {
        Settled,
        AlreadySettled,
        OrderNotFound,
        OrderNotConfirmed
    }
}
