using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.API.Filters;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Models;
using ECommerceBackend.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBackend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;

        public CheckoutController(ICheckoutService checkoutService)
        {
            _checkoutService = checkoutService;
        }

        // Step 1 of checkout: reserve stock (Redis) + create a Pending order (with a billing
        // snapshot). The [Idempotent] filter (Idempotency-Key header) makes a double-click /
        // network retry replay the same result instead of creating a second order.
        [Authorize]
        [HttpPost("begin")]
        [Idempotent]
        public async Task<IActionResult> Begin([FromBody] BeginCheckoutModel model)
        {
            try
            {
                var result = await _checkoutService.BeginCheckoutAsync(model);
                return Ok(result);
            }
            catch (InsufficientStockException ex)
            {
                return Conflict(new
                {
                    message = "Some items are out of stock.",
                    productId = ex.ProductId,
                    requestedQuantity = ex.RequestedQuantity
                });
            }
        }
    }
}
