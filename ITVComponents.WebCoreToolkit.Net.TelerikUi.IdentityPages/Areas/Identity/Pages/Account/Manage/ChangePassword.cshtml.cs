using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly IPageHandlerProvider<ChangePasswordModel, IChangePasswordHandler> handlerImpl;
        private readonly ILogger<ChangePasswordModel> _logger;
        private readonly IStringLocalizer<IdentityMessages> localizer;
        public ChangePasswordModel(
            IPageHandlerProvider<ChangePasswordModel,IChangePasswordHandler> handlerImpl,
            ILogger<ChangePasswordModel> logger,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _logger = logger;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string OldPassword { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
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
                    if (!user.HasPassword)
                    {
                        return RedirectToPage("./SetPassword");
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
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                var user = await handlerImpl.Handler.FetchUser(User);
                if (!user.UserExists)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                try
                {
                    var changePasswordResult =
                        await handlerImpl.Handler.ChangePassword(user, Input.OldPassword, Input.NewPassword);
                    if (!changePasswordResult.Succeeded)
                    {
                        foreach (var error in changePasswordResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        return Page();
                    }

                    await handlerImpl.Handler.RefreshSignIn(user);
                    _logger.LogInformation("User changed their password successfully.");
                    StatusMessage = localizer["Your password has been changed."];

                    return RedirectToPage();
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
