using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly IPageHandlerProvider<LogoutModel, ILogoutHandler> handlerImpl;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(IPageHandlerProvider<LogoutModel,ILogoutHandler> handlerImpl, ILogger<LogoutModel> logger)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                await handlerImpl.Handler.SignOut();
                _logger.LogInformation("User logged out.");
                if (returnUrl != null)
                {
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    return RedirectToPage();
                }
            }

            return NotFound();
        }
    }
}
