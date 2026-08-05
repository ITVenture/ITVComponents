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
    public interface IResetAuthenticatorHandler:IPageHandlerInstance<ResetAuthenticatorModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
        bool ReleaseUser(UserQueryTicket userTicket);
        /// <summary>
        /// Setzt den Authenticator-Schluessel zurueck. <paramref name="refreshSignIn"/> stellt dabei auch das
        /// Authentifizierungs-Cookie neu aus - das braucht eine echte HTTP-Antwort. Aufrufer auf einem
        /// Blazor-Circuit uebergeben <c>false</c> und springen ueber /Account/Manage/RefreshSignIn zurueck.
        /// </summary>
        Task ResetAuthenticator(UserQueryTicket userTicket, bool refreshSignIn = true);
    }
}
