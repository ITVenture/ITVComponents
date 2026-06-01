using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage
{
    public interface IGenerateRecoveryCodesHandler:IPageHandlerInstance<GenerateRecoveryCodesModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);

        bool ReleaseUser(UserQueryTicket userTicket);
        string GetUserId(ClaimsPrincipal user);
        Task<string[]> GenerateNewRecoveryCodes(UserQueryTicket user, int count);
    }
}
