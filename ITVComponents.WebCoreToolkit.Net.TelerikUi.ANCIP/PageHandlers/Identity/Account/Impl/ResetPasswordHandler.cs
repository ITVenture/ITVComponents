using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class ResetPasswordHandler: IResetPasswordHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(string email)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public Task<IdentityResult> ResetPassword(UserQueryTicket userTicket, string code, string password)
        {
            return Task.FromResult(IdentityResult.Failed());
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }
    }
}
