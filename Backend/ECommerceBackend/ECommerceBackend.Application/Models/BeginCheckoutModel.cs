namespace ECommerceBackend.Application.Models
{
    // Step 1 of checkout (POST /checkout/begin): reserve stock (Redis) + create a Pending order.
    // The billing/contact details are captured now so the background fulfillment worker can build
    // the invoice + email from the persisted order (not the cart).
    public class BeginCheckoutModel
    {
        public Guid UserId { get; set; }
        public required OrderDetails OrderDetails { get; set; }
    }
}
