using System;
using System.Linq;
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
    public class GenerateRecoveryCodesModel : PageModel
    {
        private readonly IPageHandlerProvider<GenerateRecoveryCodesModel, IGenerateRecoveryCodesHandler> handlerImpl;
        private readonly ILogger<GenerateRecoveryCodesModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public GenerateRecoveryCodesModel(
            IPageHandlerProvider<GenerateRecoveryCodesModel,IGenerateRecoveryCodesHandler> handlerImpl,
            ILogger<GenerateRecoveryCodesModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        [TempData]
        public string[] RecoveryCodes { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
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
                    var isTwoFactorEnabled = user.TwoFactorEnabled;
                    if (!isTwoFactorEnabled)
                    {
                        throw new InvalidOperationException(
                            $"Cannot generate recovery codes for user with ID '{userId}' because they do not have 2FA enabled.");
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
                var userId = handlerImpl.Handler.GetUserId(User);
                if (!user.UserExists)
                {
                    return NotFound(localizer["Unable to load user with ID '{0}'.", userId]);
                }

                try
                {
                    var isTwoFactorEnabled = user.TwoFactorEnabled;
                    if (!isTwoFactorEnabled)
                    {
                        throw new InvalidOperationException(
                            $"Cannot generate recovery codes for user with ID '{userId}' as they do not have 2FA enabled.");
                    }

                    var recoveryCodes = await handlerImpl.Handler.GenerateNewRecoveryCodes(user, 10);
                    RecoveryCodes = recoveryCodes;

                    _logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);
                    StatusMessage = localizer["You have generated new recovery codes."];
                    return RedirectToPage("./ShowRecoveryCodes");
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