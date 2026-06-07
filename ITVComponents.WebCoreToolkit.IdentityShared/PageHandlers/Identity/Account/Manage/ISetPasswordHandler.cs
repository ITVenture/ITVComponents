using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage
{
    public interface ISetPasswordHandler:IPageHandlerInstance<SetPasswordModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
        bool ReleaseUser(UserQueryTicket userTicket);
        Task<IdentityResult> AddPassword(UserQueryTicket userTicket, string newPassword);
        Task RefreshSignIn(UserQueryTicket userTicket);
    }
}
