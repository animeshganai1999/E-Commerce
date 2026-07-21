using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceBackend.Domain.Entities
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public Guid Id { get; set; } // = the orderId generated at checkout

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public OrderStatus Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime? ReservationExpiresAt { get; set; } // mirrors the Redis reservation TTL

        public DateTime? StockSettledAt { get; set; } // set once the outbox settles stock to SQL (idempotency guard)

        // Billing / contact snapshot captured at checkout — used by the background worker
        // to generate the invoice and send the email (so it doesn't depend on the cart).
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Address2 { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; } // optimistic concurrency token

        public List<OrderLineItem> Items { get; set; } = new();
    }
}
