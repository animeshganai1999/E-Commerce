namespace ECommerceBackend.Application.Models
{
    // Result of POST /checkout/begin. The client uses OrderId for the subsequent /payment/pay call.
    public class BeginCheckoutResult
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime ReservationExpiresAt { get; set; }
    }
}
