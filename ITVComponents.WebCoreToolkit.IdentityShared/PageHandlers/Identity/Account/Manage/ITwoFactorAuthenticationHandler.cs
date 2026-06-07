using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage
{
    public interface ITwoFactorAuthenticationHandler:IPageHandlerInstance<TwoFactorAuthenticationModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);

        bool ReleaseUser(UserQueryTicket userTicket);
        
        string GetUserId(ClaimsPrincipal user);
        Task ForgetTwoFactorClient();
    }
}
