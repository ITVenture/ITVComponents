using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Extras.EmailDnsValidation;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel : PageModel
    {
        private readonly IPageHandlerProvider<EmailModel, IEmailHandler> handlerImpl;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public EmailModel(
            IPageHandlerProvider<EmailModel,IEmailHandler> handlerImpl,
            IEmailSender emailSender,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.handlerImpl = handlerImpl;
            _emailSender = emailSender;
            this.localizer = localizer;
        }

        public string Username => handlerImpl.Handler.GetUserName(User);

        [Display(Name="Email")]
        public string Email { get; set; }

        public bool IsEmailConfirmed { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
            [Display(Name = "New email")]
            public string NewEmail { get; set; }
        }

        private void LoadAsync(UserQueryTicket userTicket)
        {
            Email = userTicket.Email;
            Input = new InputModel
            {
                NewEmail = userTicket.Email
            };

            IsEmailConfirmed = userTicket.EmailConfirmed;
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
                    LoadAsync(user);
                    return Page();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostChangeEmailAsync()
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
                    if (!ModelState.IsValid)
                    {
                        LoadAsync(user);
                        return Page();
                    }

                    var email = user.Email;
                    if (Input.NewEmail != email)
                    {
                        var mail = new EmailAddress(Input.NewEmail, new MailDomainValidator());
                        if (mail.FormatOk && mail.DomainOk)
                        {
                            var tokenData = await handlerImpl.Handler.GetMailToken(user, Input.NewEmail);
                            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(tokenData.Code));
                            var callbackUrl = Url.Page(
                                "/Account/ConfirmEmailChange",
                                pageHandler: null,
                                values: new { userId = tokenData.UserId, email = Input.NewEmail, code = code },
                                protocol: Request.Scheme);
                            await _emailSender.SendEmailAsync(
                                Input.NewEmail,
                                localizer["Confirm your email"],
                                localizer[
                                    "Hello {0}\r\nPlease confirm your account by <a href='{1}'>clicking here</a>.",
                                    Username, HtmlEncoder.Default.Encode(callbackUrl)]);

                            StatusMessage = localizer["Verification email sent. Please check your email."];
                            return RedirectToPage();
                        }
                        else
                        {
                            if (!mail.FormatOk)
                            {
                                StatusMessage = localizer["InvalidMailFormat"];
                            }
                            else if (!mail.DomainOk)
                            {
                                StatusMessage = localizer["InvalidMailDomain"];
                            }
                        }
                    }

                    StatusMessage = localizer["Your email is unchanged."];
                    return RedirectToPage();
                }
                finally
                {
                    handlerImpl.Handler.ReleaseUser(user);
                }
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
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
                    if (!ModelState.IsValid)
                    {
                        LoadAsync(user);
                        return Page();
                    }

                    var tokenData = await handlerImpl.Handler.GetMailToken(user);
                    var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(tokenData.Code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = tokenData.UserId, code = code },
                        protocol: Request.Scheme);
                    await _emailSender.SendEmailAsync(
                        user.Email,
                        localizer["Confirm your email"],
                        localizer["Hello {0}\r\nPlease confirm your account by <a href='{1}'>clicking here</a>.",
                            Username,
                            HtmlEncoder.Default.Encode(callbackUrl)]);

                    StatusMessage = localizer["Verification email sent. Please check your email."];
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
