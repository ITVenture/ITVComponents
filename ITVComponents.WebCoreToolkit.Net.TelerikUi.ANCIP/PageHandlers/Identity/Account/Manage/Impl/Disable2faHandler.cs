using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class Disable2faHandler: IDisable2faHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public Task<IdentityResult> SetTwoFactorEnabled(UserQueryTicket user, bool b)
        {
            return Task.FromResult(IdentityResult.Failed());
        }
    }
}
