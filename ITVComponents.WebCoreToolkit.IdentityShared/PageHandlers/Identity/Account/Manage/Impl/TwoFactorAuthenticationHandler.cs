using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl
{
    internal class TwoFactorAuthenticationHandler: ITwoFactorAuthenticationHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }

        public Task ForgetTwoFactorClient()
        {
            return Task.CompletedTask;
        }
    }
}
