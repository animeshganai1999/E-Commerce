using ECommerceBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBackend.API.Controllers
{
    public class PaymentRequest
    {
        public Guid OrderId { get; set; }
        public bool Success { get; set; } = true; // dummy: simulate success/failure
    }

    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(ICheckoutService checkoutService, ILogger<PaymentController> logger)
        {
            _checkoutService = checkoutService;
            _logger = logger;
        }

        // Step 2 of checkout: process the (dummy) payment for a reserved order.
        // On success -> confirm (invoice + email happen in the background outbox worker).
        // On failure -> release the reserved stock.
        [Authorize]
        [HttpPost("pay")]
        public async Task<IActionResult> Pay([FromBody] PaymentRequest request)
        {
            _logger.LogInformation("Payment attempt for order {OrderId}", request.OrderId);

            // --- Dummy payment processing ---
            // In future, verify a real payment gateway result here.
            if (!request.Success)
            {
                await _checkoutService.ReleaseStockAsync(request.OrderId);
                return StatusCode(402, new { message = "Payment failed. Reservation released." });
            }

            // Payment succeeded -> confirm the order. This writes the outbox message; the
            // background worker generates the invoice, emails it, and settles stock to SQL.
            await _checkoutService.ConfirmStockAsync(request.OrderId);

            return Ok(new { message = "Payment successful. Your order is confirmed; invoice will arrive by email shortly." });
        }
    }
}
