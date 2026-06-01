using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account.Manage
{
    public class SetPasswordModel : PageModel
    {
        private readonly IPageHandlerProvider<SetPasswordModel, ISetPasswordHandler> handlerImpl;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public SetPasswordModel(
            IPageHandlerProvider<SetPasswordModel,ISetPasswordHandler> handlerImpl,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            /*, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long."*/
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
                    var hasPassword = user.HasPassword;

                    if (hasPassword)
                    {
                        return RedirectToPage("./ChangePassword");
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
                if (user == null)
                {
                    return NotFound(
                        localizer["Unable to load user with ID '{0}'.", handlerImpl.Handler.GetUserId(User)]);
                }

                try
                {
                    var addPasswordResult = await handlerImpl.Handler.AddPassword(user, Input.NewPassword);
                    if (!addPasswordResult.Succeeded)
                    {
                        foreach (var error in addPasswordResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        return Page();
                    }

                    await handlerImpl.Handler.RefreshSignIn(user);
                    StatusMessage = localizer["Your password has been set."];

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
