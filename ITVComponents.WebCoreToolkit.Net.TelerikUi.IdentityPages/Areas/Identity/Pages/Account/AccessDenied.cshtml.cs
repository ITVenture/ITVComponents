using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account
{
    public class AccessDeniedModel : PageModel
    {
        private readonly IPageHandlerProvider<AccessDeniedModel, IAccessDeniedHandler> handlerImpl;

        public AccessDeniedModel(IPageHandlerProvider<AccessDeniedModel, IAccessDeniedHandler> handlerImpl)
        {
            this.handlerImpl = handlerImpl;
        }

        public IActionResult OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                return handlerImpl.Handler.OnGet();
            }

            return NotFound();
        }
    }
}

