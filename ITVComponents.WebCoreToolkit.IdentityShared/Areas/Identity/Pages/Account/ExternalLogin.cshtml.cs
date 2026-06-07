using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly IPageHandlerProvider<ExternalLoginModel, IExternalLoginHandler> handlerImpl;
        private readonly ILogger<ExternalLoginModel> logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ExternalLoginModel(IPageHandlerProvider<ExternalLoginModel, IExternalLoginHandler> handlerImpl,
            ILogger<ExternalLoginModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            this.logger = logger;
            this.localizer = localizer;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                return RedirectToPage("./Login");
            }

            return NotFound();
        }

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
                var properties = handlerImpl.Handler.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return new ChallengeResult(provider, properties);
            }

            return NotFound();
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                returnUrl = returnUrl ?? Url.Content("~/");
                if (remoteError != null)
                {
                    ErrorMessage = localizer["Error from external provider: {0}", remoteError];
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }

                var status = await handlerImpl.Handler.PerformExternalLogin();
                if (!status.Success && status.ErrorOnLoadExternalData)
                {
                    ErrorMessage = localizer["Error loading external login information."];
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }

                if (status.Success)
                {
                    logger.LogInformation("{Name} logged in with {LoginProvider} provider.",
                        status.LoginUserName, status.ProviderName);
                    return LocalRedirect(returnUrl);
                }

                if (status.AuthResult?.IsLockedOut??false)
                {
                    return RedirectToPage("./Lockout");
                }

                return RedirectToPage("./Login");
            }

            return NotFound();
        }
    }
}
