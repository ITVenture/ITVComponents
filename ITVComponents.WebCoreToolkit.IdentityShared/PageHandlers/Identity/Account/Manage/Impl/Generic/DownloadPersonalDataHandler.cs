using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(DownloadPersonalDataHandler))]
    internal class DownloadPersonalDataHandler<TUser>: IDownloadPersonalDataHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private UserGuard<TUser> userGuard;

        public DownloadPersonalDataHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public async Task<IDictionary<string, string>> FetchPersonalData(UserQueryTicket userTicket)
        {
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(TUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            if (userGuard.GetUser(userTicket, out var user))
            {
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }
            }

            return personalData;
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }
    }
}
