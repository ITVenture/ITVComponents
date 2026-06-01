using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account
{
    public interface IConfirmEmailHandler:IPageHandlerInstance<ConfirmEmailModel>
    {
        Task<UserQueryTicket> FetchUser(string userId);

        Task<IdentityResult> ConfirmEmailCode(UserQueryTicket userTicket, string code);

        bool ReleaseUser(UserQueryTicket userTicket);
        string RedirectOnNoCode { get; }
    }
}
