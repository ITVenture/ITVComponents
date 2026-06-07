using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ConfirmEmailHandler))]
    internal class ConfirmEmailHandler<TUser>:IConfirmEmailHandler where TUser: class
    {
        private readonly UserManager<TUser> userManager;

        private UserGuard<TUser> userGuard;

        public ConfirmEmailHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public async Task<UserQueryTicket> FetchUser(string userId)
        {
            return await userGuard.FetchUser(userId);
        }

        public async Task<IdentityResult> ConfirmEmailCode(UserQueryTicket userTicket, string code)
        {
            if (userTicket.UserExists && userGuard.GetUser(userTicket, out var user))
            {
                var result = await userManager.ConfirmEmailAsync(user, code);
                return result;
            }

            return IdentityResult.Failed();
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public string RedirectOnNoCode { get; } = "/Index";

        public bool UsePage => true;
    }
}
