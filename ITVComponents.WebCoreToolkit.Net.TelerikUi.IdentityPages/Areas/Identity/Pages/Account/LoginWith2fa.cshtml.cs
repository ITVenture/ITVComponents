using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly IPageHandlerProvider<LoginWith2faModel, ILoginWith2faHandler> handlerImpl;
        private readonly ILogger<LoginWith2faModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public LoginWith2faModel(IPageHandlerProvider<LoginWith2faModel,ILoginWith2faHandler> handlerImpl, ILogger<LoginWith2faModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(7/*, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long."*/, MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; }

            [Display(Name = "Remember this machine")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string returnUrl = null)
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
                    RememberMe = rememberMe;

                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                returnUrl = returnUrl ?? Url.Content("~/");

                var user = await handlerImpl.Handler.GetTwoFactorAuthenticationUserAsync();
                if (!user.UserExists)
                {
                    throw new InvalidOperationException(localizer["Unable to load two-factor authentication user."]);
                }

                try
                {
                    var authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
                    var userId = handlerImpl.Handler.GetUserId(user);
                    var result =
                        await handlerImpl.Handler.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe,
                            Input.RememberMachine);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", userId);
                        return LocalRedirect(returnUrl);
                    }
                    else if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User with ID '{UserId}' account locked out.", userId);
                        return RedirectToPage("./Lockout");
                    }
                    else
                    {
                        _logger.LogWarning("Invalid authenticator code entered for user with ID '{UserId}'.", userId);
                        ModelState.AddModelError(string.Empty, localizer["Invalid authenticator code."]);
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
