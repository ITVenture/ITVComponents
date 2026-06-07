using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage
{
    public class ExternalLoginsModel : PageModel
    {
        private readonly IPageHandlerProvider<ExternalLoginsModel, IExternalLoginsHandler> handlerImpl;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        public ExternalLoginsModel(
            IPageHandlerProvider<ExternalLoginsModel, IExternalLoginsHandler> handlerImpl,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            this.localizer = localizer;
        }

        public IList<UserLoginInfo> CurrentLogins { get; set; }

        public IList<AuthenticationHandlerDefinition> OtherLogins { get; set; }

        public bool ShowRemoveButton { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

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
                    var userConfig = await handlerImpl.Handler.GetUserExternalLoginConfiguration(user);
                    CurrentLogins = userConfig.ExternalUserLogins;
                    OtherLogins = userConfig.AvailableLogins;
                    ShowRemoveButton = user.HasPassword || CurrentLogins.Count > 1;
                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
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
                    var result =
                        await handlerImpl.Handler.RemoveExternalAuthentication(user, loginProvider, providerKey);
                    if (!result.Success)
                    {
                        StatusMessage = localizer["The external login was not removed."];
                        return RedirectToPage();
                    }

                    StatusMessage = localizer["The external login was removed."];
                    return RedirectToPage();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            if (handlerImpl.Handler.UsePage)
            {
                // Clear the existing external cookie to ensure a clean login process
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                // Request a redirect to the external login provider to link a login for the current user
                var redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
                var properties =
                    handlerImpl.Handler.ConfigureExternalAuthenticationProperties(provider, redirectUrl, User);
                if (properties != null)
                {
                    return new ChallengeResult(provider, properties);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            if (handlerImpl.Handler.UsePage)
            {
                var user = await handlerImpl.Handler.FetchUser(User);
                if (!user.UserExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                var result = await handlerImpl.Handler.AddExternalLogin(user);

                if (!result.Success && result.IdentityResult == null)
                {
                    throw new InvalidOperationException(
                        $"Unexpected error occurred loading external login info for user with ID '{result.UserId}'.");
                }

                if (!result.Success && result.IdentityResult != null)
                {
                    StatusMessage =
                        localizer[
                            "The external login was not added. External logins can only be associated with one account."];
                    return RedirectToPage();
                }

                // Clear the existing external cookie to ensure a clean login process
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                StatusMessage = localizer["The external login was added."];
                return RedirectToPage();
            }

            return NotFound();
        }
    }
}
