using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ConfirmEmailHandler))]
    internal class ConfirmEmailHandler<TUser>:IConfirmEmailHandler where TUser: class
    {
        // Contract shared with the onboarding join flow (AdminViews sets the same token + cookie). Must match.
        private const string JoinNonceCookie = "Onb.JoinNonce";
        private const string JoinNonceProvider = "Onboarding";
        private const string JoinNonceName = "JoinNonce";

        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;

        private UserGuard<TUser> userGuard;

        public ConfirmEmailHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public async Task<UserQueryTicket> FetchUser(string userId)
        {
            return await userGuard.FetchUser(userId);
        }

        public async Task<IdentityResult> ConfirmEmailCode(UserQueryTicket userTicket, string code)
        {
            if (userTicket.UserExists && userGuard.GetUser(userTicket, out var user))
            {
                var result = await userManager.ConfirmEmailAsync(user, code);
                return result;
            }

            return IdentityResult.Failed();
        }

        public async Task TryJoinAutoLoginAsync(UserQueryTicket userTicket, HttpContext httpContext)
        {
            var presented = httpContext?.Request.Cookies[JoinNonceCookie];
            if (string.IsNullOrEmpty(presented) || !userGuard.GetUser(userTicket, out var user))
            {
                return;
            }

            var stored = await userManager.GetAuthenticationTokenAsync(user, JoinNonceProvider, JoinNonceName);
            // Constant-time compare; FixedTimeEquals also returns false on length mismatch (e.g. no token stored).
            if (string.IsNullOrEmpty(stored) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(presented)))
            {
                return;
            }

            // Same browser that registered → consume the nonce, rotate the security stamp (invalidates the
            // confirm link for reuse) and establish the session so the invitee lands authenticated on the
            // accept page. Any other browser lacks the matching nonce and only gets the e-mail confirmed.
            await userManager.RemoveAuthenticationTokenAsync(user, JoinNonceProvider, JoinNonceName);
            await userManager.UpdateSecurityStampAsync(user);
            await signInManager.SignInAsync(user, isPersistent: false);
            httpContext.Response.Cookies.Delete(JoinNonceCookie);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public string RedirectOnNoCode { get; } = "/Index";

        public bool UsePage => true;
    }
}
