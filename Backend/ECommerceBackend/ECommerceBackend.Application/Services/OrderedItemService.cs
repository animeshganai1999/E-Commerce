using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Options;
using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace ECommerceBackend.Application.Services
{
    public class OrderedItemService : IOrderedItemService
    {

        private readonly IInvoiceRepository _invoiceRepository;
        private readonly BlobContainerClient _containerClient;
        private readonly string _containerName;

        public OrderedItemService(
            IInvoiceRepository invoiceRepository,
            BlobServiceClient blobServiceClient,
            IOptions<AzureBlobOptions> blobOptions)
        {
            _invoiceRepository = invoiceRepository;
            _containerName = blobOptions.Value.ContainerName;
            _containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        }

        public async Task<UserInvoice> GetOrCreateInvoiceAsync(
            Guid orderId,
            Guid userId,
            int numberOfItems,
            decimal totalAmount,
            CancellationToken cancellationToken)
        {
            var blobClient = _containerClient.GetBlobClient($"invoices/{orderId}.pdf");
            var invoice = new UserInvoice
            {
                OrderId = orderId,
                UserId = userId,
                InvoiceDate = DateTime.UtcNow,
                InvoiceLink = blobClient.Uri.ToString(),
                NumberOfItems = numberOfItems,
                TotalAmount = totalAmount
            };

            return await _invoiceRepository.GetOrCreateAsync(invoice);
        }

        public async Task MarkAttemptStartedAsync(
            UserInvoice invoice,
            DateTime attemptedAt,
            CancellationToken cancellationToken)
        {
            invoice.AttemptCount += 1;
            invoice.LastAttemptAt = attemptedAt;
            invoice.LastError = null;
            await _invoiceRepository.SaveChangesAsync();
        }

        public async Task UploadInvoiceAsync(
            UserInvoice invoice,
            byte[] invoiceBytes,
            DateTime uploadedAt,
            CancellationToken cancellationToken)
        {
            if (!invoice.OrderId.HasValue)
                throw new InvalidOperationException("The invoice does not have an order id.");

            await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);
            var blobClient = _containerClient.GetBlobClient($"invoices/{invoice.OrderId}.pdf");
            await using var stream = new MemoryStream(invoiceBytes);
            await blobClient.UploadAsync(
                stream,
                overwrite: true,
                cancellationToken: cancellationToken);

            invoice.InvoiceLink = blobClient.Uri.ToString();
            invoice.BlobUploadedAt = uploadedAt;
            await _invoiceRepository.SaveChangesAsync();
        }

        public async Task MarkEmailDispatchedAsync(
            UserInvoice invoice,
            DateTime dispatchedAt,
            CancellationToken cancellationToken)
        {
            invoice.EmailDispatchedAt = dispatchedAt;
            await _invoiceRepository.SaveChangesAsync();
        }

        public async Task MarkFulfilledAsync(
            UserInvoice invoice,
            DateTime fulfilledAt,
            CancellationToken cancellationToken)
        {
            invoice.FulfilledAt = fulfilledAt;
            invoice.LastError = null;
            await _invoiceRepository.SaveChangesAsync();
        }

        public async Task RecordFailureAsync(
            UserInvoice invoice,
            string error,
            DateTime attemptedAt,
            CancellationToken cancellationToken)
        {
            invoice.LastAttemptAt = attemptedAt;
            invoice.LastError = error.Length <= 2000 ? error : error[..2000];
            await _invoiceRepository.SaveChangesAsync();
        }

        public async Task<List<UserInvoice>> GetInvoicesByUserIdAsync(Guid userId)
        {
            var invoices = (await _invoiceRepository.GetInvoicesByUserIdAsync(userId)).ToList();
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

            foreach (var invoice in invoices.Where(invoice => invoice.OrderId.HasValue))
            {
                var blobClient = _containerClient.GetBlobClient($"invoices/{invoice.OrderId}.pdf");
                if (!blobClient.CanGenerateSasUri)
                {
                    throw new InvalidOperationException(
                        "The configured Blob Storage credentials cannot generate invoice download links.");
                }

                invoice.InvoiceLink = blobClient.GenerateSasUri(
                    BlobSasPermissions.Read,
                    expiresAt).ToString();
            }

            return invoices;
        }
    }
}
