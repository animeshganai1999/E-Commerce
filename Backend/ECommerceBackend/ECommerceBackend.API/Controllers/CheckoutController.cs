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

        public CheckoutController(ICheckoutService checkoutService, IEmailService emailService, IConfiguration config)
        {
            _checkoutService = checkoutService;
            _emailService = emailService;
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
            
            // Save PDF locally or send via email, etc.
            //var filePath = Path.Combine("Invoices", $"Invoice_{invoiceDataModel.UserId}.pdf");
            //Directory.CreateDirectory("Invoices");
            //await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes); // Fixed by explicitly using System.IO.File

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
