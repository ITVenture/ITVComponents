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
    internal class ChangePasswordHandler: IChangePasswordHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public bool ReleaseUser(UserQueryTicket ticket)
        {
            return true;
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }

        public Task<IdentityResult> ChangePassword(UserQueryTicket userTicket, string currentPassword, string newPassword)
        {
            return Task.FromResult(IdentityResult.Failed());
        }

        public Task RefreshSignIn(UserQueryTicket userTicket)
        {
            return Task.CompletedTask;
        }
    }
}
