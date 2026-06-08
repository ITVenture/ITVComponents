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
    }
}
