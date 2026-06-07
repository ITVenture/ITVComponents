using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl
{
    internal class IndexHandler: IIndexHandler
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

        public Task RefreshSignIn(UserQueryTicket userTicket)
        {
            return Task.CompletedTask;
        }

        public Task<IdentityResult> ChangePhoneNumber(UserQueryTicket userTicket, string newPhoneNumber)
        {
            return Task.FromResult(IdentityResult.Failed());
        }
    }
}
