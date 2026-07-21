using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("Products")]
    public class Product
    {
        [Required]
        public int Id { get; set; } // Primary key (matches the external ProductId used in CartItem)

        [Required]
        public required string Title { get; set; } // Product title

        [Required]
        public decimal Price { get; set; } // Current unit price

        public string? Description { get; set; } // Product description

        public string? Category { get; set; } // Product category (e.g., "men's clothing")

        public string? Image { get; set; } // Product image URL

        // Flattened representation of the external "rating" object.
        public double RatingRate { get; set; } // rating.rate

        public int RatingCount { get; set; } // rating.count

        [Required]
        public int StockQuantity { get; set; } // Available stock — guarded against oversell

        [Timestamp]
        public byte[]? RowVersion { get; set; } // Optimistic concurrency token, auto-managed by SQL Server.
    }
}
