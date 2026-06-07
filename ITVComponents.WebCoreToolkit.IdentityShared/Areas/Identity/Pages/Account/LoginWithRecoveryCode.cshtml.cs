using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWithRecoveryCodeModel : PageModel
    {
        
        private readonly IPageHandlerProvider<LoginWithRecoveryCodeModel, ILoginWithRecoveryCodeHandler> handlerImpl;
        private readonly ILogger<LoginWithRecoveryCodeModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public LoginWithRecoveryCodeModel(IPageHandlerProvider<LoginWithRecoveryCodeModel, ILoginWithRecoveryCodeHandler> handlerImpl, ILogger<LoginWithRecoveryCodeModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [BindProperty]
            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Recovery Code")]
            public string RecoveryCode { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                // Ensure the user has gone through the username & password screen first
                var user = await handlerImpl.Handler.GetTwoFactorAuthenticationUserAsync();
                if (!user.UserExists)
                {
                    throw new InvalidOperationException(localizer["Unable to load two-factor authentication user."]);
                }

                try
                {
                    ReturnUrl = returnUrl;

                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                var user = await handlerImpl.Handler.GetTwoFactorAuthenticationUserAsync();
                if (!user.UserExists)
                {
                    throw new InvalidOperationException(localizer["Unable to load two-factor authentication user."]);
                }

                try
                {
                    var recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty);
                    var userId = handlerImpl.Handler.GetUserId(user);
                    var result = await handlerImpl.Handler.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User with ID '{UserId}' logged in with a recovery code.", userId);
                        return LocalRedirect(returnUrl ?? Url.Content("~/"));
                    }

                    if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User with ID '{UserId}' account locked out.", userId);
                        return RedirectToPage("./Lockout");
                    }
                    else
                    {
                        _logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}' ", userId);
                        ModelState.AddModelError(string.Empty, "Invalid recovery code entered.");
                        return Page();
                    }
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
