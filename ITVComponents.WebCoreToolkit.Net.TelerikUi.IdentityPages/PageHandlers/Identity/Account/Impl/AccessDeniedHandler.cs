using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class AccessDeniedHandler:IAccessDeniedHandler
    {
        private readonly AccessDeniedModel basicPageModel;

        public AccessDeniedHandler(AccessDeniedModel basicPageModel)
        {
            this.basicPageModel = basicPageModel;
        }

        public IActionResult OnGet()
        {
            return basicPageModel.Page();
        }

        public bool UsePage => true;
    }
}
