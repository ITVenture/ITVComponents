using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly IPageHandlerProvider<ResetPasswordModel, IResetPasswordHandler> handlerImpl;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ResetPasswordModel(IPageHandlerProvider<ResetPasswordModel,IResetPasswordHandler> handlerImpl,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100/*, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long."*/, MinimumLength = 6)]
            [Display(Name = "Password")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null)
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (code == null)
                {
                    return BadRequest(localizer["A code must be supplied for password reset."]);
                }
                else
                {
                    Input = new InputModel
                    {
                        Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
                    };
                    return Page();
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

                var user = await handlerImpl.Handler.FetchUser(Input.Email);
                if (!user.UserExists)
                {
                    // Don't reveal that the user does not exist
                    return RedirectToPage("./ResetPasswordConfirmation");
                }

                try
                {
                    var result = await handlerImpl.Handler.ResetPassword(user, Input.Code, Input.Password);
                    if (result.Succeeded)
                    {
                        return RedirectToPage("./ResetPasswordConfirmation");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
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
    }
}
