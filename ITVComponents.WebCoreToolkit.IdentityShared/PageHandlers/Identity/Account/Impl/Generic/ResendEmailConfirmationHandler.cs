using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ResendEmailConfirmationHandler))]
    internal class ResendEmailConfirmationHandler<TUser>:IResendEmailConfirmationHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;

        private UserGuard<TUser> userGuard;

        public ResendEmailConfirmationHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public async Task<UserQueryTicket> FetchUser(string email)
        {
            return await userGuard.FetchUserByMail(email);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public async Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                var userId = await userManager.GetUserIdAsync(user);
                var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
                return new UserMailTokenData { UserId = userId, Code = code, Success = true };
            }

            return new UserMailTokenData { Success = false };
        }
    }
}
