using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ForgotPasswordHandler))]
    internal class ForgotPasswordHandler<TUser>:IForgotPasswordHandler where TUser:class
    {
        private readonly UserManager<TUser> userManager;

        private UserGuard<TUser> userGuard;

        public ForgotPasswordHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public async Task<UserQueryTicket> FetchUser(string email)
        {
            return await userGuard.FetchUserByMail(email);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public async Task<string> GeneratePasswordResetToken(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                return code;
            }

            return string.Empty;
        }

        public bool UsePage => true;
    }
}
