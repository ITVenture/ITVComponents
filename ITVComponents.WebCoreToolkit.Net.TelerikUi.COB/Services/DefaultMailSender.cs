using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Options;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Castle.Core.Logging.ILogger;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Services
{
    public class DefaultMailSender : IEmailSender
    {
        private readonly IServiceProvider services;
        private readonly ILogger<DefaultMailSender> logger;

        public DefaultMailSender(IServiceProvider services, ILogger<DefaultMailSender> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            using (var scp = services.CreateScope())
            {
                var settings = scp.ServiceProvider.GetService<IGlobalSettings<IdentityMailSettings>>().Value;
                if (!string.IsNullOrEmpty(settings.EmailHost) && !string.IsNullOrEmpty(settings.SenderAddress))
                {
                    MailMessage msg = new()
                    {
                        From = new MailAddress(settings.SenderAddress, settings.SenderDisplayName),
                        Body = message,
                        IsBodyHtml = true,
                        Subject = subject
                    };
                    msg.To.Add(email);
                    SmtpClient client = new SmtpClient(settings.EmailHost, settings.EmailPort);
                    if (!string.IsNullOrEmpty(settings.SenderUserName) &&
                        !string.IsNullOrEmpty(settings.SenderPassword))
                    {
                        client.Credentials =
                            new NetworkCredential(settings.SenderUserName, settings.SenderPassword.Decrypt());
                    }

                    client.EnableSsl = settings.UseSsl;
                    await client.SendMailAsync(msg);
                }
                else
                {
                    logger.LogError("Mail-Configuration is incomplete!");
                }
            }
        }
    }
}
