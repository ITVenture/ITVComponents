using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class EmailHandler : IEmailHandler
    {
        public bool UsePage => false;
        public string GetUserName(ClaimsPrincipal user)
        {
            return null;
        }

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

        public Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket, string newMail)
        {
            return Task.FromResult(new UserMailTokenData { Success = false });
        }

        public Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket)
        {
            return Task.FromResult(new UserMailTokenData { Success = false });
        }
    }
}
