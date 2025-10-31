using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace KarlixID.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Jednostavan async mail sender (SMTP).
        /// </summary>
        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            // SMTP postavke
            var smtpHost = _config["Smtp:Host"];
            var smtpPort = int.TryParse(_config["Smtp:Port"], out var port) ? port : 587;
            var smtpUser = _config["Smtp:User"];
            var smtpPass = _config["Smtp:Pass"];
            var smtpFrom = _config["Smtp:From"] ?? smtpUser;
            var smtpFromName = _config["Smtp:FromName"] ?? "KarlixID";

            if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
                throw new InvalidOperationException("SMTP postavke nisu ispravno definirane u konfiguraciji.");

            try
            {
                var fromAddress = new MailAddress(smtpFrom, smtpFromName);
                var toAddress = new MailAddress(to);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 10000 // 10 sekundi
                };

                using var msg = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                await client.SendMailAsync(msg);
            }
            catch (FormatException)
            {
                throw new FormatException($"E-mail adresa '{to}' nije u ispravnom formatu.");
            }
            catch (SmtpException ex)
            {
                throw new InvalidOperationException($"Slanje e-maila nije uspjelo: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Neočekivana greška pri slanju e-maila: {ex.Message}", ex);
            }
        }
    }
}
