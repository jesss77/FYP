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

        public async Task SendTableAllocationEmailAsync(string email, string customerName, DateTime reservationDate, TimeSpan reservationTime, int partySize, string tableInfo)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentNullException(nameof(email));

            string message = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #D4AF37, #B8941E); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .detail-row {{ margin: 15px 0; padding: 10px; background: white; border-left: 4px solid #D4AF37; }}
                        .label {{ font-weight: bold; color: #555; }}
                        .value {{ color: #333; font-size: 1.1em; }}
                        .footer {{ text-align: center; margin-top: 20px; color: #888; font-size: 0.9em; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1 style='margin: 0;'>🎉 Table Allocated!</h1>
                            <p style='margin: 10px 0 0 0;'>Your reservation has been confirmed</p>
                        </div>
                        <div class='content'>
                            <p>Dear {customerName},</p>
                            <p>Great news! Your table has been allocated for your upcoming reservation at <strong>Fine O Dine</strong>.</p>
                            
                            <div class='detail-row'>
                                <span class='label'>📅 Date:</span>
                                <span class='value'>{reservationDate:dddd, MMMM dd, yyyy}</span>
                            </div>
                            
                            <div class='detail-row'>
                                <span class='label'>🕐 Time:</span>
                                <span class='value'>{reservationTime:hh\\:mm} </span>
                            </div>
                            
                            <div class='detail-row'>
                                <span class='label'>👥 Party Size:</span>
                                <span class='value'>{partySize} {(partySize == 1 ? "guest" : "guests")}</span>
                            </div>
                            
                            <div class='detail-row'>
                                <span class='label'>🪑 Table Assignment:</span>
                                <span class='value'>{tableInfo}</span>
                            </div>
                            
                            <p style='margin-top: 30px;'>We look forward to serving you. Please arrive on time to ensure your table is ready.</p>
                            
                            <p><strong>Need to make changes?</strong> Please contact us as soon as possible.</p>
                        </div>
                        <div class='footer'>
                            <p>Fine O Dine - Where flavors meet elegance</p>
                            <p>© 2025 Fine O Dine. All Rights Reserved</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(email, "Table Allocated - Reservation Confirmed", message);
        }
    }
}