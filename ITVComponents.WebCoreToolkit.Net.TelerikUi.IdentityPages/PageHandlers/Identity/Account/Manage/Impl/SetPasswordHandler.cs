using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Dynamitey;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class SetPasswordHandler: ISetPasswordHandler
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

        public Task<IdentityResult> AddPassword(UserQueryTicket userTicket, string newPassword)
        {
            return Task.FromResult(IdentityResult.Failed());
        }

        public Task RefreshSignIn(UserQueryTicket userTicket)
        {
            return Task.CompletedTask;
        }
    }
}
