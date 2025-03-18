using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class Disable2faModel : PageModel
    {
        private readonly IPageHandlerProvider<Disable2faModel, IDisable2faHandler> handlerImpl;
        private readonly ILogger<Disable2faModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        public Disable2faModel(
            IPageHandlerProvider<Disable2faModel,IDisable2faHandler> handlerImpl,
            ILogger<Disable2faModel> logger,
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
                    if (!user.TwoFactorEnabled)
                    {

                        throw new InvalidOperationException(localizer[
                            "Cannot disable 2FA for user with ID '{0}' as it's not currently enabled.",
                            handlerImpl.Handler.GetUserId(User)]);
                    }

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
                if (!user.UserExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                try
                {
                    var disable2faResult = await handlerImpl.Handler.SetTwoFactorEnabled(user, false);
                    if (!disable2faResult.Succeeded)
                    {
                        throw new InvalidOperationException(localizer[
                            "Unexpected error occurred disabling 2FA for user with ID '{0}'.",
                            handlerImpl.Handler.GetUserId(User)]);
                    }

                    _logger.LogInformation("User with ID '{UserId}' has disabled 2fa.",
                        handlerImpl.Handler.GetUserId(User));
                    StatusMessage =
                        localizer["2fa has been disabled. You can reenable 2fa when you setup an authenticator app"];
                    return RedirectToPage("./TwoFactorAuthentication");
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