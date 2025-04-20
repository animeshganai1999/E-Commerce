using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using AutoMapper;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Interfaces;

namespace ECommerceBackend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ILogger<CartController> _logger;
        private readonly IMapper _mapper; // Used for the automapper
        private readonly ICartService _cartService; // Used for the cart service

        public CartController(ILogger<CartController> logger, IMapper mapper, ICartService cartService)
        {
            _logger = logger;
            _mapper = mapper;
            _cartService = cartService;
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateCart([FromBody] CartDiffDTO cartDiff)
        {
            await _cartService.ApplyCartDiffAsync(cartDiff);
            return Ok();
        }

        [HttpGet("getItems")]
        public async Task<IActionResult> GetCart([FromQuery] int userId)
        {
            var cartItems = await _cartService.GetCartByUserIdAsync(userId);
            if (cartItems == null || !cartItems.Any())
            {
                return NotFound("No items found in the cart.");
            }
            var cartItemsDTO = _mapper.Map<IEnumerable<CartItemDTO>>(cartItems);
            return Ok(cartItemsDTO);
        }
    }
}
