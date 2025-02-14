using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(IndexHandler))]
    internal class IndexHandler<TUser>: IIndexHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;

        private UserGuard<TUser> userGuard;

        public IndexHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user, AdditionalUserInfoToLoad.Email | AdditionalUserInfoToLoad.Phone | AdditionalUserInfoToLoad.UserName);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
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

        public Task<IdentityResult> ChangePhoneNumber(UserQueryTicket userTicket, string newPhoneNumber)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                return userManager.SetPhoneNumberAsync(user, newPhoneNumber);
            }

            return Task.FromResult(IdentityResult.Failed());
        }
    }
}
