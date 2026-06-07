using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage
{
    public interface IPersonalDataHandler:IPageHandlerInstance<PersonalDataModel>
    {
        Task<bool> UserExists(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
    }
}
