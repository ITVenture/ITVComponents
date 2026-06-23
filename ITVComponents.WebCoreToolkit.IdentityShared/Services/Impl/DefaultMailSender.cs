using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Options;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl
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
                logger.LogDebug("Preparing mail to {Recipient} (subject: {Subject}); mode={Mode}, host={Host}, dumpDir={DumpDir}.",
                    email, subject, settings.OperationMode, settings.EmailHost, settings.TestMailDumpDirectory);
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
                    SmtpClient client;
                    string destination;
                    if (settings.OperationMode == MailOperationMode.Productive)
                    {
                        client = new SmtpClient(settings.EmailHost, settings.EmailPort);
                        if (!string.IsNullOrEmpty(settings.SenderUserName) &&
                            !string.IsNullOrEmpty(settings.SenderPassword))
                        {
                            client.Credentials =
                                new NetworkCredential(settings.SenderUserName, settings.SenderPassword.Decrypt());
                        }

                        client.EnableSsl = settings.UseSsl;
                        destination = $"SMTP {settings.EmailHost}:{settings.EmailPort} (ssl={settings.UseSsl})";
                    }
                    else if (settings.OperationMode == MailOperationMode.Test &&
                             !string.IsNullOrEmpty(settings.TestMailDumpDirectory))
                    {
                        // SpecifiedPickupDirectory does NOT create the directory; a missing or relative path is the
                        // classic "test mode, no error, but no file where I look" cause — surface it explicitly.
                        var fullDir = Path.GetFullPath(settings.TestMailDumpDirectory);
                        if (!Directory.Exists(fullDir))
                        {
                            logger.LogWarning("Test-mail dump directory does not exist: configured={Configured}, resolved={Resolved}. " +
                                "SmtpClient will throw — create the directory or use an absolute path.",
                                settings.TestMailDumpDirectory, fullDir);
                        }

                        client = new SmtpClient
                        {
                            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                            PickupDirectoryLocation = settings.TestMailDumpDirectory,
                            DeliveryFormat = SmtpDeliveryFormat.International
                        };
                        destination = $"pickup directory {fullDir}";
                    }
                    else
                    {
                        logger.LogError("Invalid mail operation mode! mode={Mode}, dumpDir={DumpDir} — nothing sent to {Recipient}.",
                            settings.OperationMode, settings.TestMailDumpDirectory, email);
                        return;
                    }

                    try
                    {
                        await client.SendMailAsync(msg);
                        logger.LogInformation("Mail to {Recipient} handed to {Destination}.", email, destination);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Sending mail to {Recipient} via {Destination} failed.", email, destination);
                        throw;
                    }
                }
                else
                {
                    logger.LogError("Mail-Configuration is incomplete! host={Host}, sender={Sender} — nothing sent to {Recipient}.",
                        settings.EmailHost, settings.SenderAddress, email);
                }
            }
        }
    }
}
