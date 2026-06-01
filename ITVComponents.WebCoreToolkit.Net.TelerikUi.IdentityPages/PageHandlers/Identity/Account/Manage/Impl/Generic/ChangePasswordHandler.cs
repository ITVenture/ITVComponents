using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(ChangePasswordHandler))]
    internal class ChangePasswordHandler<TUser>: IChangePasswordHandler where TUser:class
    {
        private UserGuard<TUser> userGuard;
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;

        public ChangePasswordHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;
        public async Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return await userGuard.FetchUser(user);
        }

        public bool ReleaseUser(UserQueryTicket ticket)
        {
            return userGuard.ReleaseUser(ticket);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public async Task RefreshSignIn(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                await signInManager.RefreshSignInAsync(user);
            }
        }

        public async Task<IdentityResult> ChangePassword(UserQueryTicket userTicket, string currentPassword, string newPassword)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                return await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            }

            return IdentityResult.Failed();
        }
    }
}
