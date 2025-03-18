using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage
{
    public interface IChangePasswordHandler:IPageHandlerInstance<ChangePasswordModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);

        bool ReleaseUser(UserQueryTicket ticket);
        string GetUserId(ClaimsPrincipal user);

        Task<IdentityResult> ChangePassword(UserQueryTicket userTicket, string currentPassword, string newPassword);
        Task RefreshSignIn(UserQueryTicket userTicket);
    }
}
