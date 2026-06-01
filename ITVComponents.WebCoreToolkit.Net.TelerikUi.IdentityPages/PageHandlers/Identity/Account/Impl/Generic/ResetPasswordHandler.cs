using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ResetPasswordHandler))]
    internal class ResetPasswordHandler<TUser>: IResetPasswordHandler where TUser : class
    {
        private UserGuard<TUser> userGuard;
        private readonly UserManager<TUser> userManager;

        public ResetPasswordHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public async Task<UserQueryTicket> FetchUser(string email)
        {
            return await userGuard.FetchUserByMail(email);
        }

        public async Task<IdentityResult> ResetPassword(UserQueryTicket userTicket, string code, string password)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                return await userManager.ResetPasswordAsync(user, code, password);
            }

            return IdentityResult.Failed();
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }
    }
}
