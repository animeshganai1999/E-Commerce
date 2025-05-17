using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.Models
{
    public class OrderDetails
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        public required string Address { get; set; }
        public string Address2 { get; set; }
        public required string Country { get; set; }
        public required string State { get; set; }
        public required string Zip { get; set; }
    }
}
