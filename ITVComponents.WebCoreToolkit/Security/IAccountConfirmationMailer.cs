using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Sends the e-mail-confirmation message for a freshly created account. Implemented by the identity
    /// layer (which owns the mail transport and the confirmation page), so account-creating flows that live
    /// outside the identity pages — e.g. the direct tenant onboarding — can trigger the confirmation mail
    /// without taking a dependency on the Identity-UI package.
    /// </summary>
    public interface IAccountConfirmationMailer
    {
        /// <summary>
        /// Generates a fresh confirmation token for the user and mails a confirmation link.
        /// </summary>
        /// <param name="userId">id of the created (unconfirmed) user</param>
        /// <param name="confirmPageAbsoluteUri">absolute URL of the ConfirmEmail page (e.g. https://host/Account/ConfirmEmail)</param>
        /// <param name="returnUrl">app-relative path to return to after confirmation (may be null)</param>
        /// <param name="ct">a cancellation token</param>
        /// <returns>true if a mail was sent; false if the user could not be found</returns>
        Task<bool> SendConfirmationAsync(string userId, string confirmPageAbsoluteUri, string returnUrl, CancellationToken ct = default);
    }
}
