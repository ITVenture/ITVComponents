using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailChangeModel : PageModel
    {
        private readonly IPageHandlerProvider<ConfirmEmailChangeModel, IConfirmEmailChangeHandler> handlerImpl;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        private readonly IOptions<LoginOptions> loginOptions;

        public ConfirmEmailChangeModel(IPageHandlerProvider<ConfirmEmailChangeModel, IConfirmEmailChangeHandler> handlerImpl, IStringLocalizer<IdentityMessages> localizer, 
            IOptions<LoginOptions> loginOptions)
        {
            this.handlerImpl = handlerImpl;
            this.localizer = localizer;
            this.loginOptions = loginOptions;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public bool UsernameIsEmail => loginOptions.Value.UserNameIsEmail;

        public async Task<IActionResult> OnGetAsync(string userId, string email, string code)
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (userId == null || email == null || code == null)
                {
                    return RedirectToPage(handlerImpl.Handler.RedirectOnNoCode);
                }

                var user = await handlerImpl.Handler.FetchUser(userId);
                if (!user.UserExists)
                {
                    return NotFound(localizer["Unable to load user with ID '{0}'.", userId]);
                }

                try
                {
                    code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                    var result = await handlerImpl.Handler.ChangeEmailAddress(user, email, code, UsernameIsEmail);
                    if (!result.Succeeded)
                    {
                        StatusMessage = localizer["Error changing email."];
                        return Page();
                    }

                    await handlerImpl.Handler.RefreshSignIn(user);
                    StatusMessage = localizer["Thank you for confirming your email change."];
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
