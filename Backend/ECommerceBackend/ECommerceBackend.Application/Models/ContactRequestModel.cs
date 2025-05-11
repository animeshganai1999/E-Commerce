using System.ComponentModel.DataAnnotations;

namespace ECommerceBackend.Application.Models
{
    public class ContactRequestModel
    {
        public required string Name { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        public required string Message { get; set; }
    }
}
