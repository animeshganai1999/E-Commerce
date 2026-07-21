namespace ECommerceBackend.Application.DTOs
{
    public class ProductRatingDTO
    {
        public double Rate { get; set; }
        public int Count { get; set; }
    }

    public class ProductResponseDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Image { get; set; }
        public int StockQuantity { get; set; }
        public ProductRatingDTO Rating { get; set; } = new();
    }
}
