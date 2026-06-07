using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl
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
