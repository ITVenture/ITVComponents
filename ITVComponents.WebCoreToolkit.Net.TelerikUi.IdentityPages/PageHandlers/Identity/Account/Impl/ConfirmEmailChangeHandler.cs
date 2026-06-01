using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class ConfirmEmailChangeHandler:IConfirmEmailChangeHandler
    {
        public Task<UserQueryTicket> FetchUser(string userId)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false, UserResultId = Guid.Empty });
        }

        public Task<IdentityResult> ChangeEmailAddress(UserQueryTicket userTicket, string email, string code, bool changeUsernameAlso)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
                { Code = "0", Description = "Email-Change is not available without a configured User-Manager" }));
        }

        public Task RefreshSignIn(UserQueryTicket userTicket)
        {
            return Task.CompletedTask;
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public string RedirectOnNoCode { get; } = "/Index";

        public bool UsePage => false;
    }
}
