using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetInvoiceByUserId()
        {
            var invoices = await _orderedItemService.GetInvoicesByUserIdAsync(User.GetRequiredUserId());
            if (invoices == null || !invoices.Any())
            {
                return NotFound("No invoices found for this user.");
            }
            return Ok(invoices);
        }
    }
 }
