using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class DeletePersonalDataHandler: IDeletePersonalDataHandler
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

        public bool ReleaseUser(UserQueryTicket user)
        {
            return true;
        }

        public Task<bool> CheckPassword(UserQueryTicket userTicket, string password)
        {
            return Task.FromResult(false);
        }

        public Task<UserLogoutResult> DeleteAccount(UserQueryTicket userTicket)
        {
            return Task.FromResult(new UserLogoutResult
                { IdentityResult = IdentityResult.Failed(), LoggedOut = false, UserDeleted = false });
        }
    }
}
