using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly IPageHandlerProvider<ResendEmailConfirmationModel, IResendEmailConfirmationHandler> handlerImpl;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ResendEmailConfirmationModel(IPageHandlerProvider<ResendEmailConfirmationModel,IResendEmailConfirmationHandler> handlerImpl, IEmailSender emailSender,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _emailSender = emailSender;
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
        }

        public IActionResult OnGet()
        {
            if (handlerImpl.Handler.UsePage)
            {
                return Page();
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
                    ModelState.AddModelError(string.Empty,
                        localizer["Verification email sent. Please check your email."]);
                    return Page();
                }

                try
                {
                    var mailToken = await handlerImpl.Handler.GetMailToken(user);
                    if (mailToken.Success)
                    {
                        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(mailToken.Code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { userId = mailToken.UserId, code = code },
                            protocol: Request.Scheme);
                        await _emailSender.SendEmailAsync(
                            Input.Email,
                            localizer["Confirm your email"],
                            localizer["Please confirm your account by <a href='{0}'>clicking here</a>.",
                                HtmlEncoder.Default.Encode(callbackUrl)]);
                    }

                    ModelState.AddModelError(string.Empty,
                        localizer["Verification email sent. Please check your email."]);
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
