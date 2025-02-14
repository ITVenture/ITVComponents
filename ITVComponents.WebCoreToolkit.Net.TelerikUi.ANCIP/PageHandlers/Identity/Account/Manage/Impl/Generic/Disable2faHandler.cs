using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(Disable2faHandler))]
    internal class Disable2faHandler<TUser>: IDisable2faHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;

        private UserGuard<TUser> userGuard;
        public Disable2faHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public async Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return await userGuard.FetchUser(user);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public bool ReleaseUser(UserQueryTicket ticket)
        {
            return userGuard.ReleaseUser(ticket);
        }

        public async Task<IdentityResult> SetTwoFactorEnabled(UserQueryTicket userTicket, bool b)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                return await userManager.SetTwoFactorEnabledAsync(user, false);
            }

            return IdentityResult.Failed();
        }
    }
}
