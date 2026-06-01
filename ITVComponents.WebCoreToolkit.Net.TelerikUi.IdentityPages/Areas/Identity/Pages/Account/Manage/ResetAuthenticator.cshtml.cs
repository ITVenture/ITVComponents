using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class ResetAuthenticatorModel : PageModel
    {
        private readonly IPageHandlerProvider<ResetAuthenticatorModel, IResetAuthenticatorHandler> handlerImpl;
        ILogger<ResetAuthenticatorModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ResetAuthenticatorModel(
            IPageHandlerProvider<ResetAuthenticatorModel, IResetAuthenticatorHandler> handlerImpl,
            ILogger<ResetAuthenticatorModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var user = await handlerImpl.Handler.FetchUser(User);
                if (!user.UserExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                try
                {
                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var user = await handlerImpl.Handler.FetchUser(User);
                var userId = handlerImpl.Handler.GetUserId(User);
                if (!user.UserExists)
                {
                    return NotFound(localizer["Unable to load user with ID '{0}'.", userId]);
                }

                try
                {
                    await handlerImpl.Handler.ResetAuthenticator(user);
                    _logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", userId);

                    StatusMessage =
                        localizer[
                            "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key."];

                    return RedirectToPage("./EnableAuthenticator");
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