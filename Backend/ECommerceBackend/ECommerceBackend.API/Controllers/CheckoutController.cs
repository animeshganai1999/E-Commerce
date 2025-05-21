using ECommerceBackend.Application.Interfaces;
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
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly IOrderedItemService _orderedItemService;

        public CheckoutController(ICheckoutService checkoutService, IEmailService emailService, IOrderedItemService orderedItemService, IConfiguration config)
        {
            _checkoutService = checkoutService;
            _emailService = emailService;
            _orderedItemService = orderedItemService;
            _config = config;
        }

        [Authorize]
        [HttpPost("generate-invoice")]
        public async Task<IActionResult> Checkout([FromBody] InvoiceDataModel invoiceDataModel)
        {
            Console.WriteLine($"Checkout : UserId: {invoiceDataModel.UserId}");
            var pdfBytes = await _checkoutService.GenerateInvoiceAsync(invoiceDataModel);

            // Send the Invoive over the mail
            var (isSuccess, errorMessage) = await _emailService.SendEmailAsync(_config, pdfBytes: pdfBytes, ReceiverEmail: invoiceDataModel.OrderDetails.Email);

            // Fetch all the ordered items (To find the number of orderes and total amount)
            List<OrderItem> orderItems = await _checkoutService.FetchAllIetmsAsync(invoiceDataModel.UserId);

            _orderedItemService.HandleInvoice(invoiceDataModel.UserId, pdfBytes, orderItems.Count, orderItems.Sum(i => i.TotalPrice) + 30).Wait();

            if (isSuccess)
            {
                return Ok("Ordere placed and Invoice sent successfully over Email.");
            }
            else
            {
                return StatusCode(500, $"Error sending email: {errorMessage}");
            }
        }
    }
}
