using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage
{
    /// <summary>
    /// Marker for <see cref="ISignInSessionHandler"/>. Unlike its siblings this is NOT a Razor page model — the
    /// handler serves endpoints, not a page. <see cref="IPageHandlerInstance{TPageModel}"/> puts no constraint on
    /// the marker type; it only keys the registration.
    /// </summary>
    public sealed class SignInSessionModel
    {
    }

    /// <summary>
    /// The three operations of the account area that write an authentication cookie and therefore need a real HTTP
    /// response — they cannot run over a Blazor circuit ("headers are read-only"). The account pages render
    /// interactively and call this through a short redirect: do the work on the circuit, navigate to the endpoint
    /// with <c>forceLoad</c>, come back. Splitting them out of the per-page handlers is what lets every visible
    /// page be interactive; the pages themselves no longer need <c>HttpContext</c>.
    /// </summary>
    public interface ISignInSessionHandler : IPageHandlerInstance<SignInSessionModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);

        bool ReleaseUser(UserQueryTicket userTicket);

        /// <summary>
        /// Re-issues the authentication cookie from the user's current state. Needed after anything that changes
        /// the security stamp (password, 2FA, external logins) — without it the stamp validator signs the user out
        /// at its next interval.
        /// </summary>
        Task RefreshSignIn(UserQueryTicket userTicket);

        /// <summary>Ends the session (after the account was deleted).</summary>
        Task SignOut();

        /// <summary>Drops the "remember this browser" cookie for two-factor authentication.</summary>
        Task ForgetTwoFactorClient();
    }
}
