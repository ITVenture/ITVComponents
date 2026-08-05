using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage
{
    public interface IExternalLoginsHandler:IPageHandlerInstance<ExternalLoginsModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
        Task<UserExternalLoginConfiguration> GetUserExternalLoginConfiguration(UserQueryTicket user);
        bool ReleaseUser(UserQueryTicket user);

        /// <summary>
        /// Entfernt eine externe Anmeldung. <paramref name="refreshSignIn"/> stellt dabei auch das
        /// Authentifizierungs-Cookie neu aus - das braucht eine echte HTTP-Antwort. Aufrufer auf einem
        /// Blazor-Circuit uebergeben <c>false</c> und springen ueber /Account/Manage/RefreshSignIn zurueck.
        /// </summary>
        Task<UserExternalLoginStatus> RemoveExternalAuthentication(UserQueryTicket userTicket, string loginProvider,
            string providerKey, bool refreshSignIn = true);

        AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string redirectUrl, ClaimsPrincipal user);
        Task<UserExternalLoginStatus> AddExternalLogin(UserQueryTicket userTicket);
    }
}
