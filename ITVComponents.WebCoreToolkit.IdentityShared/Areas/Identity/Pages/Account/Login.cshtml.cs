using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Options;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly IPageHandlerProvider<LoginModel, ILoginHandler> handlerImpl;
        private readonly ILogger<LoginModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        private readonly IOptions<LoginOptions> loginOptions;

        public LoginModel(IPageHandlerProvider<LoginModel,ILoginHandler> handlerImpl, 
            ILogger<LoginModel> logger,
            IStringLocalizer<IdentityMessages> localizer,
            IOptions<LoginOptions> loginOptions)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
            this.loginOptions = loginOptions;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationHandlerDefinition> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        public UserRegistrationInfo RegistrationInfo => handlerImpl.Handler.RegistrationInfo ??
                                                        new UserRegistrationInfo { AllowRegister = false };

        public ExternalLoginConfig ExternalLoginConfig => handlerImpl.Handler.ExternalLoginConfig ??
                                                          new ExternalLoginConfig { UseExternalLogins = false };

        [TempData]
        public string ErrorMessage { get; set; }

        public bool UsernameIsEmail => loginOptions.Value.UserNameIsEmail;
        public bool UseLocalAccounts => loginOptions.Value.UseLocalAccounts;

        public class InputModel
        {
            //[Required]
            [Display(Name="Email")]
            [EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
            public string Email { get; set; }

            [Display(Name = "Username")]
            public string Username { get; set; }

            [Required]
            [Display(Name = "Password")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                {
                    ModelState.AddModelError(string.Empty, ErrorMessage);
                }

                returnUrl ??= Url.Content("~/");

                // Clear the existing external cookie to ensure a clean login process
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                var tmp = await handlerImpl.Handler.FetchExternalProviders();
                ExternalLogins = tmp;

                ReturnUrl = returnUrl;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                returnUrl ??= Url.Content("~/");

                var tmp = await handlerImpl.Handler.FetchExternalProviders();
                ExternalLogins = tmp;

                if (ModelState.IsValid)
                {
                    // This doesn't count login failures towards account lockout
                    // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                    var userName = UsernameIsEmail ? Input.Email : Input.Username;
                    var result =
                        await handlerImpl.Handler.LoginUserWithPassword(userName, Input.Password, Input.RememberMe);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User logged in.");
                        return LocalRedirect(returnUrl);
                    }

                    if (result.RequiresTwoFactor)
                    {
                        return RedirectToPage("./LoginWith2fa",
                            new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                    }

                    if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User account locked out.");
                        return RedirectToPage("./Lockout");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, localizer["Invalid login attempt."]);
                        return Page();
                    }
                }

                // If we got this far, something failed, redisplay form
                return Page();
            }

            return NotFound();
        }
    }
}
