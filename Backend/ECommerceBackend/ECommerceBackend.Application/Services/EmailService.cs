using ECommerceBackend.Application.Interfaces;
using ECommerceBackend.Application.Models;
using System.Net.Mail;
using System.Net;

namespace ECommerceBackend.Application.Services
{
    public class EmailService : IEmailService
    {
        public async Task<(bool isSuccess, string errorMessage)> SendEmailAsync(IConfiguration config, ContactRequestModel request)
        {
            try
            {
                var senderEmail = config["EmailSettings:SenderEmail"];
                var appPassword = config["EmailSettings:AppPassword"];
                var receiverEmail = config["EmailSettings:ReceiverEmail"];

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
                    From = new MailAddress(senderEmail),
                    Subject = $"Contact Form Submission from {request.Name}",
                    Body = $"From: {request.Name} <{request.Email}>\n\n{request.Message}",
                    IsBodyHtml = false,
                };

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
