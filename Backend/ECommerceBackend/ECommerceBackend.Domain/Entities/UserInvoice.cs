namespace ECommerceBackend.Domain.Entities
{
    [Tags("CustomerInvoice")]
    public class UserInvoice
    {
        public Guid Id { get; set; } // Primary key (GUID)
        public Guid? OrderId { get; set; }
        public Guid UserId { get; set; } // Foreign key to the User entity
        public DateTime InvoiceDate { get; set; } // The date and time when the invoice was created
        public int NumberOfItems { get; set; } // The number of items in the invoice
        public decimal TotalAmount { get; set; } // The total amount of the invoice
        public required string InvoiceLink { get; set; } // The link to the invoice
        public DateTime? BlobUploadedAt { get; set; }
        public DateTime? EmailDispatchedAt { get; set; }
        public DateTime? FulfilledAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
    }
}
