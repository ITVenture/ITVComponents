using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl
{
    /// <summary>
    /// Default <see cref="IAppMailSender"/>: a thin pass-through to the toolkit's own
    /// <see cref="IEmailSender"/> (DefaultMailSender). Keeps the Identity-UI dependency contained to this
    /// assembly so callers (e.g. the Blazor onboarding views) depend only on the core abstraction.
    /// </summary>
    public class AppMailSender : IAppMailSender
    {
        private readonly IEmailSender emailSender;
        private readonly ILogger<AppMailSender> logger;

        public AppMailSender(IEmailSender emailSender, ILogger<AppMailSender> logger)
        {
            this.emailSender = emailSender;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public Task SendMailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => emailSender.SendEmailAsync(toEmail, subject, htmlBody);

        /// <inheritdoc/>
        /// <remarks>
        /// Attachments travel <b>past</b> <see cref="IEmailSender"/>: its signature is fixed by Identity-UI and
        /// cannot carry them. The registered transport is asked for the toolkit's own
        /// <see cref="IAttachmentMailSender"/> instead — <c>DefaultMailSender</c> implements both, a host that
        /// replaced the transport with an attachment-less one gets a clear refusal rather than a mail whose
        /// files quietly went missing.
        /// </remarks>
        public Task SendMailAsync(string toEmail, string subject, string htmlBody,
            IReadOnlyList<MailAttachment> attachments, CancellationToken ct = default)
        {
            if (attachments is not { Count: > 0 })
            {
                return SendMailAsync(toEmail, subject, htmlBody, ct);
            }

            if (emailSender is IAttachmentMailSender attachmentSender)
            {
                return attachmentSender.SendEmailAsync(toEmail, subject, htmlBody, attachments, ct);
            }

            logger.LogError(
                "Mail to {Recipient} carries {AttachmentCount} attachment(s), but the configured transport " +
                "'{Transport}' does not implement IAttachmentMailSender — nothing sent.",
                toEmail, attachments.Count, emailSender.GetType().FullName);
            throw new NotSupportedException(
                $"The configured mail-transport '{emailSender.GetType().FullName}' does not support attachments.");
        }
    }
}
