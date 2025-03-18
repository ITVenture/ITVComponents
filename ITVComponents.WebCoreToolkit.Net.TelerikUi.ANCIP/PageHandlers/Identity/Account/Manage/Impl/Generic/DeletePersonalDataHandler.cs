using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(DeletePersonalDataHandler))]
    internal class DeletePersonalDataHandler<TUser>: IDeletePersonalDataHandler where TUser : class
    {
        private UserGuard<TUser> userGuard;
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;

        public DeletePersonalDataHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
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

        public async Task<bool> CheckPassword(UserQueryTicket userTicket, string password)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                return await userManager.CheckPasswordAsync(user, password);
            }

            return false;
        }

        public async Task<UserLogoutResult> DeleteAccount(UserQueryTicket userTicket)
        {
            var retVal = new UserLogoutResult();
            if (userGuard.GetUser(userTicket, out var user))
            {
                retVal.UserId = await userManager.GetUserIdAsync(user);
                var result = await userManager.DeleteAsync(user);
                retVal.IdentityResult = result;
                if (result.Succeeded)
                {
                    retVal.UserDeleted = true;
                    await signInManager.SignOutAsync();
                    retVal.LoggedOut = true;
                }
            }

            return retVal;
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }
    }
}
