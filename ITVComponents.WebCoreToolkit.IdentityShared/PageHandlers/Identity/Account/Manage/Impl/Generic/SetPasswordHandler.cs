using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(SetPasswordHandler))]
    internal class SetPasswordHandler<TUser>: ISetPasswordHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;
        private UserGuard<TUser> userGuard;

        public SetPasswordHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public async Task<IdentityResult> AddPassword(UserQueryTicket userTicket, string newPassword)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                return await userManager.AddPasswordAsync(user, newPassword);
            }

            // Das Ticket kennt den Benutzer nicht - ein anderer Fall als "Identity hat abgelehnt", aber von
            // aussen ohne Meldung nicht zu unterscheiden, weil beides ein Failed ohne Fehlerliste ist.
            LogEnvironment.LogEvent(
                $"{nameof(AddPassword)} called with a ticket that resolves to no user - nothing was changed.",
                LogSeverity.Error);
            return IdentityResult.Failed();
        }

        public async Task RefreshSignIn(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                await signInManager.RefreshSignInAsync(user);
            }
        }
    }
}
