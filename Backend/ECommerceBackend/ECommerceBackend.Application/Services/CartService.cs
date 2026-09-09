using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Application.Constants;
using ECommerceBackend.Application.Exceptions;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;

namespace ECommerceBackend.Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly ICartCache _cartCache;
        private readonly IProductRepository _productRepository;

        public CartService(
            ICartRepository cartRepository,
            ICartCache cartCache,
            IProductRepository productRepository)
        {
            _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
            _cartCache = cartCache ?? throw new ArgumentNullException(nameof(cartCache));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        }

        public async Task ApplyCartDiffAsync(Guid userId, CartDiffDTO diff)
        {
            ValidateDiff(diff);

            await _cartRepository.ExecuteInSerializableTransactionAsync(async () =>
            {
                var requestedItems = diff.Added.Concat(diff.Updated).ToList();
                var currentItems = (await _cartRepository.GetCartByUserIdAsync(userId)).ToList();
                var existingProductId = diff.Added
                    .Select(item => item.ProductId)
                    .Cast<int?>()
                    .FirstOrDefault(productId => currentItems.Any(
                        current => current.ProductId == productId!.Value));
                if (existingProductId.HasValue)
                {
                    throw new RequestValidationException(
                        nameof(CartDiffDTO.Added),
                        $"Product {existingProductId.Value} is already in the cart; update its quantity instead.");
                }

                var resultingProductIds = currentItems.Select(item => item.ProductId).ToHashSet();
                resultingProductIds.ExceptWith(diff.Removed.Select(item => item.ProductId));
                resultingProductIds.UnionWith(diff.Added.Select(item => item.ProductId));
                if (resultingProductIds.Count > CartPolicy.MaxDistinctItems)
                {
                    throw new RequestValidationException(
                        nameof(CartDiffDTO.Added),
                        $"A cart can contain at most {CartPolicy.MaxDistinctItems} distinct products.");
                }

                var products = (await _productRepository.GetProductsByIdsAsync(
                        requestedItems.Select(item => item.ProductId)))
                    .ToDictionary(product => product.Id);

                var missingProductId = requestedItems
                    .Select(item => item.ProductId)
                    .Cast<int?>()
                    .FirstOrDefault(productId => !products.ContainsKey(productId!.Value));
                if (missingProductId.HasValue)
                {
                    throw new RequestValidationException(
                        nameof(CartItemDTO.ProductId),
                        $"Product {missingProductId.Value} was not found.");
                }

                var addedItems = diff.Added.Select(item =>
                {
                    var product = products[item.ProductId];
                    return new CartItem
                    {
                        UserId = userId,
                        Description = product.Title,
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    };
                });

                await _cartRepository.AddRangeAsync(addedItems);

                foreach (var item in diff.Updated)
                {
                    var existing = await _cartRepository.GetAsync(
                        x => x.UserId == userId && x.ProductId == item.ProductId);
                    if (existing != null)
                    {
                        existing.Quantity = item.Quantity;
                        await _cartRepository.UpdateAsync(existing);
                    }
                }

                foreach (var item in diff.Removed)
                {
                    await _cartRepository.DeleteAsync(
                        x => x.UserId == userId && x.ProductId == item.ProductId);
                }

                await _cartRepository.SaveChangesAsync();
            });

            await _cartCache.InvalidateAsync(userId);
        }

        private static void ValidateDiff(CartDiffDTO diff)
        {
            if (diff.Added is null || diff.Updated is null || diff.Removed is null)
            {
                throw new RequestValidationException(
                    nameof(CartDiffDTO),
                    "Added, Updated, and Removed collections are required.");
            }

            var allItems = diff.Added.Concat(diff.Updated).Concat(diff.Removed).ToList();
            if (allItems.Any(item => item.ProductId <= 0))
            {
                throw new RequestValidationException(
                    nameof(CartItemDTO.ProductId),
                    "Product identifiers must be greater than zero.");
            }

            var invalidQuantity = diff.Added.Concat(diff.Updated)
                .FirstOrDefault(item => item.Quantity is <= 0 or > CartPolicy.MaxQuantityPerItem);
            if (invalidQuantity is not null)
            {
                throw new RequestValidationException(
                    nameof(CartItemDTO.Quantity),
                    $"Quantity must be between 1 and {CartPolicy.MaxQuantityPerItem}.");
            }

            var duplicateProductId = allItems
                .GroupBy(item => item.ProductId)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateProductId.HasValue)
            {
                throw new RequestValidationException(
                    nameof(CartItemDTO.ProductId),
                    $"Product {duplicateProductId.Value} appears in more than one cart operation.");
            }
        }

        public async Task<IEnumerable<CartItem>> GetCartByUserIdAsync(Guid userId)
        {
            // Cache-aside: serve from Redis on a hit, else load from SQL and populate the cache.
            var cached = await _cartCache.GetByUserAsync(userId);
            if (cached != null)
                return cached;

            var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);

            var items = cartItems.ToList();
            await _cartCache.SetByUserAsync(userId, items);

            return items;
        }
        //public async Task DeleteCartItemsAsync(Guid userId)
        //{
        //    // Fetch the cart items for the given user ID
        //    var cartItems = await _cartRepository.GetCartByUserIdAsync(userId);
        //    if (cartItems == null || !cartItems.Any())
        //        throw new Exception("No items found in the cart.");
        //    // Delete all cart items for the given user ID
        //    foreach (var item in cartItems)
        //    {
        //        await _cartRepository.DeleteAsync(x => x.UserId == userId && x.ProductId == item.ProductId);
        //    }
        //    await _cartRepository.SaveChangesAsync();
        //}
    }
}
