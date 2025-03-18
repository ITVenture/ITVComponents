using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account
{
    public interface IResetPasswordHandler:IPageHandlerInstance<ResetPasswordModel>
    {
        Task<UserQueryTicket> FetchUser(string email);
        Task<IdentityResult> ResetPassword(UserQueryTicket userTicket, string code, string password);
        bool ReleaseUser(UserQueryTicket userTicket);
    }
}
