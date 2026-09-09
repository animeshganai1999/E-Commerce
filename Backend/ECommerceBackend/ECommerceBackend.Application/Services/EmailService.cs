using ECommerceBackend.Application.Interfaces;
using System.Net.Mail;
using System.Net;

namespace ECommerceBackend.Application.Services
{
    public class EmailService : IEmailService
    {
        public async Task SendInvoiceEmailAsync(
            IConfiguration config,
            byte[] pdfBytes,
            string receiverEmail,
            CancellationToken cancellationToken)
        {
            var senderEmail = config["EmailSettings:SenderEmail"];
            var appPassword = config["EmailSettings:AppPassword"];

            if (string.IsNullOrWhiteSpace(senderEmail)
                || string.IsNullOrWhiteSpace(appPassword)
                || string.IsNullOrWhiteSpace(receiverEmail))
                throw new InvalidOperationException("Email settings are not properly configured.");

            using var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(senderEmail, appPassword),
                EnableSsl = true,
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = "Your Shop Core invoice",
                Body = "Your invoice is attached.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(receiverEmail);

            using var pdfStream = new MemoryStream(pdfBytes);
            mailMessage.Attachments.Add(new Attachment(pdfStream, "Invoice.pdf", "application/pdf"));

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        }
    }
}
