using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly IPageHandlerProvider<ForgotPasswordModel, IForgotPasswordHandler> handlerImpl;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public ForgotPasswordModel(IPageHandlerProvider<ForgotPasswordModel,IForgotPasswordHandler> handlerImpl, IEmailSender emailSender, IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _emailSender = emailSender;
            this.localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name="Email")]
            [Required]
            [EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (handlerImpl.Handler.UsePage)
            {
                if (ModelState.IsValid)
                {
                    var user = await handlerImpl.Handler.FetchUser(Input.Email);
                    if (!user.UserExists || !user.EmailConfirmed)
                    {
                        // Don't reveal that the user does not exist or is not confirmed
                        return RedirectToPage("./ForgotPasswordConfirmation");
                    }

                    try
                    {
                        // For more information on how to enable account confirmation and password reset please 
                        // visit https://go.microsoft.com/fwlink/?LinkID=532713
                        var code = await handlerImpl.Handler.GeneratePasswordResetToken(user);
                        if (!string.IsNullOrEmpty(code))
                        {
                            //var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                            var callbackUrl = Url.Page(
                                "/Account/ResetPassword",
                                pageHandler: null,
                                values: new { area = "Identity", code },
                                protocol: Request.Scheme);

                            await _emailSender.SendEmailAsync(
                                Input.Email,
                                localizer["Reset Password"],
                                localizer[
                                    "ResetPasswordMailBody",
                                    HtmlEncoder.Default.Encode(callbackUrl)]);

                            return RedirectToPage("./ForgotPasswordConfirmation");
                        }
                    }
                    finally
                    {
                        handlerImpl.Handler.ReleaseUser(user);
                    }
                }

                return Page();
            }

            return NotFound();
        }
    }
}
