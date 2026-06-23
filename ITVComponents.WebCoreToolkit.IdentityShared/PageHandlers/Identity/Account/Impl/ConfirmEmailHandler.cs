using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl
{
    internal class ConfirmEmailHandler:IConfirmEmailHandler
    {
        public Task<UserQueryTicket> FetchUser(string userId)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false, UserResultId = Guid.Empty });
        }

        public Task<IdentityResult> ConfirmEmailCode(UserQueryTicket userTicket, string code)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
                { Code = "0", Description = "Confirm is not available without a configured User-Manager" }));
        }

        public Task TryJoinAutoLoginAsync(UserQueryTicket userTicket, HttpContext httpContext) => Task.CompletedTask;

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public string RedirectOnNoCode { get; } = "/Index";

        public bool UsePage => false;
    }
}
