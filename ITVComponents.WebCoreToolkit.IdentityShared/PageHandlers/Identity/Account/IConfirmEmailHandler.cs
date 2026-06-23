using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account
{
    public interface IConfirmEmailHandler:IPageHandlerInstance<ConfirmEmailModel>
    {
        Task<UserQueryTicket> FetchUser(string userId);

        Task<IdentityResult> ConfirmEmailCode(UserQueryTicket userTicket, string code);

        /// <summary>
        /// Opt-in seamless sign-in for the employee-invitation join flow: when the confirming request carries a
        /// valid join-nonce cookie that matches the nonce parked on the just-confirmed user (i.e. this is the same
        /// browser that registered), establishes the session, rotates the security stamp (single-use) and clears
        /// the nonce. A missing/mismatched nonce is a no-op, so a confirm link opened in any other browser only
        /// confirms the e-mail and never grants a session.
        /// </summary>
        Task TryJoinAutoLoginAsync(UserQueryTicket userTicket, HttpContext httpContext);

        bool ReleaseUser(UserQueryTicket userTicket);
        string RedirectOnNoCode { get; }
    }
}
