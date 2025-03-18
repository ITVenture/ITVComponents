using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class EnableAuthenticatorModel : PageModel
    {
        private readonly IPageHandlerProvider<EnableAuthenticatorModel, IEnableAuthenticatorHandler> handlerImpl;
        private readonly ILogger<EnableAuthenticatorModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public EnableAuthenticatorModel(
            IPageHandlerProvider<EnableAuthenticatorModel, IEnableAuthenticatorHandler> handlerImpl,
            ILogger<EnableAuthenticatorModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        public string SharedKey { get; set; }

        public string AuthenticatorUri { get; set; }

        [TempData]
        public string[] RecoveryCodes { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputMd Input { get; set; }

        public class InputMd
        {
            [Required]
            [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Verification Code")]
            public string Code { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
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
                    await LoadSharedKeyAndQrCodeUriAsync(user);

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
                    if (!ModelState.IsValid)
                    {
                        await LoadSharedKeyAndQrCodeUriAsync(user);
                        return Page();
                    }

                    // Strip spaces and hypens
                    var verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

                    var authenticatorVerificationResult =
                        await handlerImpl.Handler.VerifyAuthenticatorToken(user, verificationCode);

                    if (!authenticatorVerificationResult.Success)
                    {
                        ModelState.AddModelError("Input.Code", localizer["Verification code is invalid."]);
                        await LoadSharedKeyAndQrCodeUriAsync(user);
                        return Page();
                    }


                    _logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.",
                        authenticatorVerificationResult.UserId);

                    StatusMessage = localizer["Your authenticator app has been verified."];

                    if (authenticatorVerificationResult.RecoveryCodeCount == 0)
                    {
                        var recoveryCodes = await handlerImpl.Handler.GenerateNewRecoveryCodes(user, 10);
                        RecoveryCodes = recoveryCodes;
                        return RedirectToPage("./ShowRecoveryCodes");
                    }
                    else
                    {
                        return RedirectToPage("./TwoFactorAuthentication");
                    }
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        private async Task LoadSharedKeyAndQrCodeUriAsync(UserQueryTicket userTicket)
        {
            var initData = await handlerImpl.Handler.LoadSharedKeyAndQrData(userTicket);
            if (initData.Success)
            {
                AuthenticatorUri = initData.AuthenticatorUri;
                SharedKey = initData.SharedKey;
            }
        }
    }
}
