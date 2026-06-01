using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
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
                var retVal = await userManager.AddPasswordAsync(user, newPassword);
            }

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
