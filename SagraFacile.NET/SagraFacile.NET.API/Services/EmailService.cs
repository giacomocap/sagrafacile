using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SagraFacile.NET.API.Services.Interfaces;
using Microsoft.Extensions.Logging; // Added for logging

namespace SagraFacile.NET.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger; // Added logger

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger) // Injected logger
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var email = new MimeMessage();
                email.Sender = MailboxAddress.Parse(_emailSettings.SenderEmail);
                if (!string.IsNullOrEmpty(_emailSettings.SenderName))
                {
                    email.Sender.Name = _emailSettings.SenderName;
                }
                email.From.Add(email.Sender); // Set From address
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;

                var builder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };
                email.Body = builder.ToMessageBody();

                using var smtp = new SmtpClient();

                // Determine SecureSocketOptions based on UseSsl setting and port
                SecureSocketOptions socketOptions;
                if (_emailSettings.UseSsl)
                {
                    // Common ports for SSL/TLS are 465 (Implicit SSL) and 587 (STARTTLS)
                    socketOptions = _emailSettings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                }
                else
                {
                    socketOptions = SecureSocketOptions.None; // Or StartTlsWhenAvailable if preferred for opportunistic TLS
                }

                _logger.LogInformation("Connecting to SMTP server {Host}:{Port} using {SocketOptions}", _emailSettings.SmtpHost, _emailSettings.SmtpPort, socketOptions);
                await smtp.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, socketOptions);

                // Only authenticate if username/password are provided
                if (!string.IsNullOrEmpty(_emailSettings.Username) && !string.IsNullOrEmpty(_emailSettings.Password))
                {
                    _logger.LogInformation("Authenticating with username {Username}", _emailSettings.Username);
                    await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                }
                else
                {
                    _logger.LogInformation("No SMTP credentials provided, attempting anonymous connection.");
                }


                _logger.LogInformation("Sending email to {ToEmail} with subject '{Subject}'", toEmail, subject);
                await smtp.SendAsync(email);
                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);

                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}. Subject: {Subject}", toEmail, subject);
                // Depending on requirements, you might re-throw, return a status, or just log.
                // For now, we just log the error.
            }
        }
    }
}
