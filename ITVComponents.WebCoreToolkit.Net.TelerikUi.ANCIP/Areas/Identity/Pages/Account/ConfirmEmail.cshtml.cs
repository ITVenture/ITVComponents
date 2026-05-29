using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account;
//using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly IPageHandlerProvider<ConfirmEmailModel, IConfirmEmailHandler> handlerImpl;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ConfirmEmailModel(IPageHandlerProvider<ConfirmEmailModel, IConfirmEmailHandler> handlerImpl, IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            this.localizer = localizer;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (userId == null || code == null)
                {
                    return RedirectToPage(handlerImpl.Handler.RedirectOnNoCode);
                }

                var user = await handlerImpl.Handler.FetchUser(userId); //_userManager.FindByIdAsync(userId);
                if (!user.UserExists)
                {
                    return NotFound(localizer["Unable to load user with ID '{0}'.", userId]);
                }

                try
                {
                    code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                    var result = await handlerImpl.Handler.ConfirmEmailCode(user, code);
                    StatusMessage = result.Succeeded
                        ? localizer["Thank you for confirming your email."]
                        : localizer["Error confirming your email."];
                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }
    }
}
