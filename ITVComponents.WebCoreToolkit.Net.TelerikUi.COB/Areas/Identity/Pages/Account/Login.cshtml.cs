using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        private readonly IOptions<AuthenticationHandlerOptions> availableAuthenticators;

        public LoginModel(SignInManager<User> signInManager, 
            ILogger<LoginModel> logger,
            UserManager<User> userManager,
            IStringLocalizer<IdentityMessages> localizer,
            IOptions<AuthenticationHandlerOptions> availableAuthenticators)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            this.localizer = localizer;
            this.availableAuthenticators = availableAuthenticators;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationHandlerDefinition> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name="Email")]
            [EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Password")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            var tmp = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            var providers = (from t in tmp
                join a in availableAuthenticators.Value.AuthenticationHandlers
                    on t.Name equals a.AuthenticationSchemeName into j
                from c in j.DefaultIfEmpty()
                where c?.DisplayInHandlerSelection ?? true
                select c ?? new AuthenticationHandlerDefinition
                {
                    AuthenticationSchemeName = t.Name,
                    DisplayInHandlerSelection = true,
                    DisplayName = t.DisplayName,
                    LogoFile = $"/images/logo/login-logo{t.DisplayName}.png"
                }).ToList();
            ExternalLogins = providers;

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var tmp = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            var providers = (from t in tmp
                join a in availableAuthenticators.Value.AuthenticationHandlers
                    on t.Name equals a.AuthenticationSchemeName into j
                from c in j.DefaultIfEmpty()
                where c?.DisplayInHandlerSelection ?? true
                select c ?? new AuthenticationHandlerDefinition
                {
                    AuthenticationSchemeName = t.Name,
                    DisplayInHandlerSelection = true,
                    DisplayName = t.DisplayName,
                    LogoFile = $"/images/logo/login-logo{t.DisplayName}.png"
                }).ToList();
            ExternalLogins = providers;

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
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
    }
}
