using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account
{
    public interface IAccessDeniedHandler:IPageHandlerInstance<AccessDeniedModel>
    {
        IActionResult OnGet();
    }
}
