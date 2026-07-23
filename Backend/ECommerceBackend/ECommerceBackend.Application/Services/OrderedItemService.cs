using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Options;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace ECommerceBackend.Application.Services
{
    public class OrderedItemService : IOrderedItemService
    {

        public readonly IInvoiceRepository _invoiceRepository;
        private readonly string _blobConnectionString;
        private readonly string _containerName;
        public OrderedItemService(IInvoiceRepository invoiceRepository, IOptions<AzureBlobOptions> blobOptions)
        {
            _invoiceRepository = invoiceRepository;
            _blobConnectionString = blobOptions.Value.ConnectionString;
            _containerName = blobOptions.Value.ContainerName;
        }
        public async Task SaveInvoiceUrlToDB(Guid userId, string pdfUrl, int NumberOfItems, decimal TotalAmount)
        {
            var invoice = new UserInvoice
            {
                UserId = userId,
                InvoiceDate = DateTime.UtcNow,
                InvoiceLink = pdfUrl,
                NumberOfItems = NumberOfItems,
                TotalAmount = TotalAmount
            };
            await _invoiceRepository.AddAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();
        }
        public async Task<bool> HandleInvoice(Guid userId, byte[] InvoiceBytes, int NumberOfItems, decimal TotalAmount)
        {
            string folder = $"{DateTime.UtcNow:yyyy/MM/dd}";
            string fileName = $"Invoice_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            string blobPath = $"{folder}/{fileName}";

            var blobServiceClient = new BlobServiceClient(_blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
            var blobClient = containerClient.GetBlobClient(blobPath);

            using (var stream = new MemoryStream(InvoiceBytes))
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            string pdfUrl = blobClient.Uri.ToString();

            // Save the PDF URL in the database
            await SaveInvoiceUrlToDB(userId, pdfUrl, NumberOfItems, TotalAmount);

            // Return true to indicate success
            return true;
        }

        public async Task<List<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId)
        {
            return (List<UserInvoice>)await _invoiceRepository.GetInvoicesByUserIdAsync(userId);
        }
    }
}
