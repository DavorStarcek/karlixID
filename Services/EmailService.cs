using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace KarlixID.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body, List<(string filename, byte[] content)> attachments = null)
        {
            var smtp = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]))
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Pass"])
            };

            var mail = new MailMessage(_config["Smtp:From"], to, subject, body);
            mail.IsBodyHtml = true;

            if (attachments != null)
            {
                foreach (var (filename, content) in attachments)
                {
                    var stream = new MemoryStream(content);
                    mail.Attachments.Add(new Attachment(stream, filename));
                }
            }

            await smtp.SendMailAsync(mail);
        }
    }
}
