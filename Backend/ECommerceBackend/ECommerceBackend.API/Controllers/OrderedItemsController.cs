using ECommerceBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace ECommerceBackend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderedItemsController : ControllerBase
    {
        private readonly IOrderedItemService _orderedItemService;
        public OrderedItemsController(IOrderedItemService orderedItemService)
        {
            _orderedItemService = orderedItemService;
        }

        [Authorize]
        [HttpGet("get-invoice")]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> GetInvoiceByUserId([FromQuery] Guid userId)
        {
            // Extract userId from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid jwtUserId))
            {
                return Unauthorized("You are not authorized to access these invoices.");
            }
            var invoices = await _orderedItemService.GetInvoicesByUserIdAsync(jwtUserId);
            if (invoices == null || !invoices.Any())
            {
                return NotFound("No invoices found for this user.");
            }
            return Ok(invoices);
        }
    }
 }
