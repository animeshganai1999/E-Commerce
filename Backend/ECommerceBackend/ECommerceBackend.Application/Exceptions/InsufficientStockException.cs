namespace ECommerceBackend.Application.Exceptions
{
    /// <summary>
    /// Thrown when an order cannot be fulfilled because a product does not have enough stock.
    /// </summary>
    public class InsufficientStockException : Exception
    {
        public int ProductId { get; }
        public int RequestedQuantity { get; }

        public InsufficientStockException(int productId, int requestedQuantity)
            : base($"Insufficient stock for product {productId}. Requested quantity: {requestedQuantity}.")
        {
            ProductId = productId;
            RequestedQuantity = requestedQuantity;
        }
    }
}
