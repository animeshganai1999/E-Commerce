using AutoMapper;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBackend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IMapper _mapper;

        public ProductsController(IProductService productService, IMapper mapper)
        {
            _productService = productService;
            _mapper = mapper;
        }

        // Public catalog endpoint — returns a page of products with live stock from our own DB.
        // Optionally filtered by category (server-side, so it scales to lakhs of products).
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? category = null)
        {
            var result = await _productService.GetProductsAsync(page, pageSize, category);
            var response = new
            {
                Items = _mapper.Map<IEnumerable<ProductResponseDTO>>(result.Items),
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages
            };
            return Ok(response);
        }

        // "Load more" catalog endpoint — keyset (seek) pagination. Pass the "afterId" returned
        // as "nextCursor" from the previous call to fetch the next batch. Performance stays
        // constant regardless of depth (unlike OFFSET-based paging).
        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed(
            [FromQuery] int? afterId = null,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? category = null)
        {
            var result = await _productService.GetProductsByCursorAsync(afterId, pageSize, category);
            var response = new
            {
                Items = _mapper.Map<IEnumerable<ProductResponseDTO>>(result.Items),
                result.NextCursor,
                result.HasMore,
                result.PageSize
            };
            return Ok(response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound($"Product {id} not found.");
            }
            var response = _mapper.Map<ProductResponseDTO>(product);
            return Ok(response);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("warmup")]
        public async Task<IActionResult> WarmUp([FromBody] int[] productIds)
        {
            await _productService.WarmUpProductsAsync(productIds);
            return Ok(new { warmed = productIds.Length });
        }
    }
}
