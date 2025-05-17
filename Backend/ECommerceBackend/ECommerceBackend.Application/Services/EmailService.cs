using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using System.Net.Mail;
using System.Net;
using Microsoft.IdentityModel.Tokens;

namespace ECommerceBackend.Application.Services
{
    public class EmailService : IEmailService
    {
        public async Task<(bool isSuccess, string errorMessage)> SendEmailAsync(IConfiguration config, ContactRequestModel? request = null, byte[]? pdfBytes = null, string? ReceiverEmail = null)
        {
            try
            {
                var senderEmail = config["EmailSettings:SenderEmail"];
                var appPassword = config["EmailSettings:AppPassword"];
                var receiverEmail = ReceiverEmail ?? config["EmailSettings:ReceiverEmail"];

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword) || string.IsNullOrWhiteSpace(receiverEmail))
                {
                    return (false, "Email settings are not properly configured.");
                }

                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(senderEmail, appPassword),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail)
                };

                if (request != null)
                {
                    // Contact form
                    mailMessage.Subject = $"Contact Form Submission from {request.Name}";
                    mailMessage.Body = $"From: {request.Name} <{request.Email}>\n\n{request.Message}";
                    mailMessage.IsBodyHtml = false;
                }
                else if (pdfBytes != null)
                {
                    // PDF attachment
                    mailMessage.Subject = "Please find the attached document";
                    mailMessage.Body = "Invoice attached.";
                    var pdfStream = new MemoryStream(pdfBytes);
                    var attachment = new Attachment(pdfStream, "Invoice.pdf", "application/pdf");
                    mailMessage.Attachments.Add(attachment);
                }
                else
                {
                    return (false, "Either request or pdfBytes must be provided.");
                }

                mailMessage.To.Add(receiverEmail);

                await smtpClient.SendMailAsync(mailMessage);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
