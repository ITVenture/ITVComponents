using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(GenerateRecoveryCodesHandler))]
    internal class GenerateRecoveryCodesHandler<TUser>: IGenerateRecoveryCodesHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;

        private UserGuard<TUser> userGuard;

        public GenerateRecoveryCodesHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public async Task<string[]> GenerateNewRecoveryCodes(UserQueryTicket userTicket, int count)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                var retVal = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, count);
                return retVal.ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
