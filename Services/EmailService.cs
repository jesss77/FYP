using FYP.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace FYP.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> options)
        {
            _emailSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentNullException(nameof(email));

            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentNullException(nameof(subject));

            if (string.IsNullOrWhiteSpace(htmlMessage))
                throw new ArgumentNullException(nameof(htmlMessage));

            try
            {
                using var client = new SmtpClient(_emailSettings.Host, _emailSettings.Port)
                {
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                    EnableSsl = _emailSettings.EnableSSL
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mail.To.Add(email);
                await client.SendMailAsync(mail);
            }
            catch (SmtpException ex)
            {
                // Log the exception here
                throw new InvalidOperationException($"Failed to send email to {email}", ex);
            }
        }

        public async Task SendConfirmationEmailAsync(ApplicationUser user, string token)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new ArgumentException("User email is required", nameof(user));

            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callbackUrl = $"{_emailSettings.ConfirmationUrl}?userId={user.Id}&code={encodedToken}";

            string message = $@"
                <html>
                <body>
                    <h2>Email Confirmation</h2>
                    <p>Thank you for registering. Please confirm your account by clicking the link below:</p>
                    <br>
                    <p><a href='{callbackUrl}' style='padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Confirm Email</a></p>
                    <br><br>
                </body>
                </html>";

            await SendEmailAsync(user.Email, "Confirm your email", message);
        }
    }
}