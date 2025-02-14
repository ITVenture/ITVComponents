using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class ForgotPasswordHandler:IForgotPasswordHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(string inputEmail)
        {
            return Task.FromResult(new UserQueryTicket
                { EmailConfirmed = false, UserExists = false, UserResultId = Guid.Empty });
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public Task<string> GeneratePasswordResetToken(UserQueryTicket userTicket)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
