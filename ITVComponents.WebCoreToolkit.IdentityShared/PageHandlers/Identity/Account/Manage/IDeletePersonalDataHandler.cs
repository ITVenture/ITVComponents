using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage
{
    public interface IDeletePersonalDataHandler:IPageHandlerInstance<DeletePersonalDataModel>
    {
        Task<UserQueryTicket> FetchUser(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
        bool ReleaseUser(UserQueryTicket user);
        Task<bool> CheckPassword(UserQueryTicket userTicket, string password);
        /// <summary>
        /// Loescht das Konto. <paramref name="signOut"/> beendet dabei auch die Sitzung — das schreibt ein Cookie
        /// und braucht darum eine echte HTTP-Antwort. Aufrufer auf einem Blazor-Circuit uebergeben <c>false</c> und
        /// erledigen die Abmeldung ueber den Endpunkt <c>/Account/Manage/SignOutSession</c>.
        /// </summary>
        Task<UserLogoutResult> DeleteAccount(UserQueryTicket userTicket, bool signOut = true);
    }
}
