using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class TwoFactorAuthenticationModel : PageModel
    {
        private const string AuthenicatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}";

        private readonly IPageHandlerProvider<TwoFactorAuthenticationModel, ITwoFactorAuthenticationHandler> handlerImpl;
        private readonly ILogger<TwoFactorAuthenticationModel> _logger;

        public TwoFactorAuthenticationModel(
            IPageHandlerProvider<TwoFactorAuthenticationModel,ITwoFactorAuthenticationHandler> handlerImpl,
            ILogger<TwoFactorAuthenticationModel> logger)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
        }

        public bool HasAuthenticator { get; set; }

        public int RecoveryCodesLeft { get; set; }

        [BindProperty]
        public bool Is2faEnabled { get; set; }

        public bool IsMachineRemembered { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var user = await handlerImpl.Handler.FetchUser(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{handlerImpl.Handler.GetUserId(User)}'.");
            }

            try
            {
                HasAuthenticator = user.IsAuthenticatorConfigured;
                Is2faEnabled = user.TwoFactorEnabled;
                IsMachineRemembered = user.MachineRememberForTwoFactor;
                RecoveryCodesLeft = user.RecoveryCodesLeft;

                return Page();
            }
            finally
            {
                handlerImpl.Handler.ReleaseUser(user);
            }
        }

        public async Task<IActionResult> OnPost()
        {
            var user = await handlerImpl.Handler.FetchUser(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{handlerImpl.Handler.GetUserId(User)}'.");
            }

            try
            {
                await handlerImpl.Handler.ForgetTwoFactorClient();
                StatusMessage =
                    "The current browser has been forgotten. When you login again from this browser you will be prompted for your 2fa code.";
                return RedirectToPage();
            }
            finally
            {
                handlerImpl.Handler.ReleaseUser(user);
            }
        }
    }
}