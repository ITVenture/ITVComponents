using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.Pages.Account.Manage
{
    public class ExternalLoginsModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IOptions<AuthenticationHandlerOptions> availableAuthenticators;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        public ExternalLoginsModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IOptions<AuthenticationHandlerOptions> availableAuthenticators,
            IStringLocalizer<IdentityMessages> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            this.availableAuthenticators = availableAuthenticators;
            this.localizer = localizer;
        }

        public IList<UserLoginInfo> CurrentLogins { get; set; }

        public IList<AuthenticationHandlerDefinition> OtherLogins { get; set; }

        public bool ShowRemoveButton { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(localizer["Unable to load user with ID '{0}'.", _userManager.GetUserId(User)]);
            }

            CurrentLogins = await _userManager.GetLoginsAsync(user);
            var tmp = (await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();
            OtherLogins = (from t in tmp
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
            ShowRemoveButton = user.PasswordHash != null || CurrentLogins.Count > 1;
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(localizer["Unable to load user with ID '{0}'.", _userManager.GetUserId(User)]);
            }

            var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                StatusMessage = localizer["The external login was not removed."];
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = localizer["The external login was removed."];
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // Request a redirect to the external login provider to link a login for the current user
            var redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, _userManager.GetUserId(User));
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(localizer["Unable to load user with ID '{0}'.", _userManager.GetUserId(User)]);
            }

            var info = await _signInManager.GetExternalLoginInfoAsync(user.Id);
            if (info == null)
            {
                throw new InvalidOperationException($"Unexpected error occurred loading external login info for user with ID '{user.Id}'.");
            }

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                StatusMessage = localizer["The external login was not added. External logins can only be associated with one account."];
                return RedirectToPage();
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            StatusMessage = localizer["The external login was added."];
            return RedirectToPage();
        }
    }
}
