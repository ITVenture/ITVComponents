using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account
{
    public interface IResetPasswordHandler:IPageHandlerInstance<ResetPasswordModel>
    {
        Task<UserQueryTicket> FetchUser(string email);
        Task<IdentityResult> ResetPassword(UserQueryTicket userTicket, string code, string password);
        bool ReleaseUser(UserQueryTicket userTicket);
    }
}
