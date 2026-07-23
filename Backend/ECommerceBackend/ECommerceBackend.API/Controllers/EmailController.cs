using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBackend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        IEmailService _emailService;
        private readonly IConfiguration _config;
        public EmailController(IEmailService emailService, IConfiguration config)
        {
            _emailService = emailService;
            _config = config;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] ContactRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request.");
            }
            var (isSuccess, errorMessage) = await _emailService.SendEmailAsync(_config, request : request);
            if (isSuccess)
            {
                return Ok("Email sent successfully.");
            }
            else
            {
                return StatusCode(500, $"Error sending email: {errorMessage}");
            }
        }
    }
}
