using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// The toolkit's own mail transport for messages that carry attachments.
    /// </summary>
    /// <remarks>
    /// This exists <b>beside</b> <c>Microsoft.AspNetCore.Identity.UI.Services.IEmailSender</c>, not on top of
    /// it: that interface has the fixed signature <c>SendEmailAsync(string, string, string)</c> and cannot be
    /// extended, so an attachment has to travel past it. A transport implements this interface in addition to
    /// <c>IEmailSender</c> (the toolkit's <c>DefaultMailSender</c> does); <see cref="IAppMailSender"/> asks the
    /// registered transport for it and reports a clean <see cref="System.NotSupportedException"/> when the host
    /// replaced the transport with one that cannot carry attachments.
    /// </remarks>
    public interface IAttachmentMailSender
    {
        /// <summary>
        /// Sends an HTML mail with the given attachments.
        /// </summary>
        /// <param name="email">recipient address</param>
        /// <param name="subject">mail subject</param>
        /// <param name="message">HTML message body</param>
        /// <param name="attachments">the attachments to carry; may be null or empty</param>
        /// <param name="ct">a cancellation token</param>
        Task SendEmailAsync(string email, string subject, string message,
            IReadOnlyList<MailAttachment> attachments, CancellationToken ct = default);
    }
}
