using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Sends an arbitrary transactional HTML mail through the application's configured mail transport.
    /// Implemented by the identity layer (which owns the SMTP settings), so flows outside the identity
    /// pages — e.g. tenant invitations — can send mail without taking a dependency on the Identity-UI
    /// package. Mirrors <see cref="IAccountConfirmationMailer"/> but for free-form messages.
    /// </summary>
    public interface IAppMailSender
    {
        /// <summary>
        /// Sends an HTML mail to the given recipient.
        /// </summary>
        /// <param name="toEmail">recipient address</param>
        /// <param name="subject">mail subject</param>
        /// <param name="htmlBody">HTML message body</param>
        /// <param name="ct">a cancellation token</param>
        Task SendMailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);

        /// <summary>
        /// Sends an HTML mail with attachments to the given recipient.
        /// </summary>
        /// <remarks>
        /// Implemented on the interface so an existing implementer keeps compiling: it then behaves like the
        /// attachment-less overload for an empty list and refuses the rest loudly instead of silently dropping
        /// the files. An implementer whose transport carries attachments (the toolkit's <c>AppMailSender</c>
        /// does) overrides this.
        /// </remarks>
        /// <param name="toEmail">recipient address</param>
        /// <param name="subject">mail subject</param>
        /// <param name="htmlBody">HTML message body</param>
        /// <param name="attachments">the attachments to carry; may be null or empty</param>
        /// <param name="ct">a cancellation token</param>
        Task SendMailAsync(string toEmail, string subject, string htmlBody,
            IReadOnlyList<MailAttachment> attachments, CancellationToken ct = default)
            => attachments is not { Count: > 0 }
                ? SendMailAsync(toEmail, subject, htmlBody, ct)
                : throw new NotSupportedException(
                    $"The registered mail-sender '{GetType().FullName}' does not support attachments.");
    }
}
