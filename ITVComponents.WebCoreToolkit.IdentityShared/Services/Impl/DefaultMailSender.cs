using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Options;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl
{
    public class DefaultMailSender : IEmailSender, IAttachmentMailSender
    {
        private readonly IServiceProvider services;
        private readonly ILogger<DefaultMailSender> logger;

        public DefaultMailSender(IServiceProvider services, ILogger<DefaultMailSender> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string message)
            => SendCoreAsync(email, subject, message, null, CancellationToken.None);

        /// <inheritdoc/>
        public Task SendEmailAsync(string email, string subject, string message,
            IReadOnlyList<MailAttachment> attachments, CancellationToken ct = default)
            => SendCoreAsync(email, subject, message, attachments, ct);

        /// <summary>
        /// The single send-path of this transport. Both entry points run through here on purpose: the three
        /// operation modes (productive SMTP, pickup-directory, misconfiguration) would otherwise drift apart,
        /// and a test-mode that only the attachment-less path knows is exactly the kind of difference that
        /// surfaces in production.
        /// </summary>
        private async Task SendCoreAsync(string email, string subject, string message,
            IReadOnlyList<MailAttachment> attachments, CancellationToken ct)
        {
            using (var scp = services.CreateScope())
            {
                var attachmentCount = attachments?.Count ?? 0;
                var settings = scp.ServiceProvider.GetService<IGlobalSettings<IdentityMailSettings>>().Value;
                logger.LogDebug("Preparing mail to {Recipient} (subject: {Subject}); mode={Mode}, host={Host}, dumpDir={DumpDir}, attachments={AttachmentCount}.",
                    email, subject, settings.OperationMode, settings.EmailHost, settings.TestMailDumpDirectory, attachmentCount);
                if (!string.IsNullOrEmpty(settings.EmailHost) && !string.IsNullOrEmpty(settings.SenderAddress))
                {
                    using MailMessage msg = new()
                    {
                        From = new MailAddress(settings.SenderAddress, settings.SenderDisplayName),
                        Body = message,
                        IsBodyHtml = true,
                        Subject = subject
                    };
                    msg.To.Add(email);

                    // The Attachment takes ownership of the stream; disposing the MailMessage disposes both.
                    for (var i = 0; i < attachmentCount; i++)
                    {
                        var attachment = attachments[i];
                        msg.Attachments.Add(new Attachment(new MemoryStream(attachment.Content),
                            attachment.FileName, attachment.ContentType));
                    }

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

                    using (client)
                    {
                        try
                        {
                            await client.SendMailAsync(msg, ct);
                            logger.LogInformation("Mail to {Recipient} handed to {Destination} (attachments={AttachmentCount}).",
                                email, destination, attachmentCount);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Sending mail to {Recipient} via {Destination} failed.", email, destination);
                            throw;
                        }
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
