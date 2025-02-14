using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage
{
    public interface IEnableAuthenticatorHandler:IPageHandlerInstance<EnableAuthenticatorModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
        bool ReleaseUser(UserQueryTicket userTicket);
        Task<AuthenticatorInitData> LoadSharedKeyAndQrData(UserQueryTicket userTicket);
        Task<AuthenticatorVerificationData> VerifyAuthenticatorToken(UserQueryTicket userTicket, string verificationCode);
        Task<string[]> GenerateNewRecoveryCodes(UserQueryTicket userTicket, int count);
    }
}
