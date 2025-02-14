using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(EmailHandler))]
    internal class EmailHandler<TUser>:IEmailHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;

        private UserGuard<TUser> userGuard;

        public EmailHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;
        public string GetUserName(ClaimsPrincipal user)
        {
            return userManager.GetUserName(user);
        }

        public async Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return await userGuard.FetchUser(user, AdditionalUserInfoToLoad.Email);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public async Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket, string newMail)
        {
            var retVal = new UserMailTokenData();
            if (userGuard.GetUser(userTicket, out var user))
            {
                var userId = await userManager.GetUserIdAsync(user);
                var code = await userManager.GenerateChangeEmailTokenAsync(user, newMail);
                retVal.Success = true;
                retVal.Code = code;
                retVal.UserId = userId;
            }

            return retVal;
        }

        public async Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket)
        {
            var retVal = new UserMailTokenData();
            if (userGuard.GetUser(userTicket, out var user))
            {
                var userId = await userManager.GetUserIdAsync(user);
                var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
                retVal.Success = true;
                retVal.Code = code;
                retVal.UserId = userId;
            }

            return retVal;
        }
    }
}
