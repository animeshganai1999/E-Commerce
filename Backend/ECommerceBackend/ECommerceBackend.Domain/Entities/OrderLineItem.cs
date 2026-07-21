using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("OrderItems")]
    public class OrderLineItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; } // reserved quantity — source of truth for confirm/release/settle

        [Required]
        public decimal UnitPrice { get; set; } // price snapshot at order time

        public required string Description { get; set; } // snapshot of the product name

        public Order Order { get; set; } = null!;
    }
}
