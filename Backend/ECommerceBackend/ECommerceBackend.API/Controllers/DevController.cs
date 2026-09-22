using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBackend.API.Controllers
{
    // DEV/TEST ONLY endpoints for load-testing convenience. Every action is blocked unless
    // the app is running in the Development environment, so it can never run in production.
    [Route("api/dev")]
    [ApiController]
    public class DevController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IWebHostEnvironment _env;

        public DevController(IProductService productService, IWebHostEnvironment env)
        {
            _productService = productService;
            _env = env;
        }

        // Flush Redis (stock, reservations, caches, locks) and re-seed stock from SQL.
        // Gives a clean baseline before a parallel load test.
        //   POST /api/dev/reset-stock
        [HttpPost("reset-stock")]
        public async Task<IActionResult> ResetStock()
        {
            if (!_env.IsDevelopment())
                return NotFound(); // hide the endpoint entirely outside Development

            // Even in Development, require an Admin. The check runs after the env gate so the
            // endpoint stays invisible (404) in production instead of leaking a 401/403.
            if (!User.IsInRole(UserRoles.Admin))
                return Forbid();

            var count = await _productService.ResetStockAsync();
            return Ok(new { message = "Redis flushed and stock re-seeded from SQL.", productsWarmed = count });
        }
    }
}
