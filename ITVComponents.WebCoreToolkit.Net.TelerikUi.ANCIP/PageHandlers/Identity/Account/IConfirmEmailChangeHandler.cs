using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account
{
    public interface IConfirmEmailChangeHandler : IPageHandlerInstance<ConfirmEmailChangeModel>
    {
        Task<UserQueryTicket> FetchUser(string userId);

        Task<IdentityResult> ChangeEmailAddress(UserQueryTicket userTicket, string email, string code, bool changeUsernameAlso);

        Task RefreshSignIn(UserQueryTicket userTicket);

        bool ReleaseUser(UserQueryTicket userTicket);
        string RedirectOnNoCode { get; }
    }
}
