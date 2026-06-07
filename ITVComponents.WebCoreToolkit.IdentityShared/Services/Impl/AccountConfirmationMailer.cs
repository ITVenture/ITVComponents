using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl
{
    /// <summary>
    /// Default <see cref="IAccountConfirmationMailer"/>: generates an e-mail-confirmation token via the
    /// <see cref="UserManager{TUser}"/> and sends the confirmation link through the toolkit's own
    /// <see cref="IEmailSender"/> (DefaultMailSender). Generic over the host's user type so this assembly
    /// stays free of any concrete user model.
    /// </summary>
    /// <typeparam name="TUser">the host's Identity user type</typeparam>
    public class AccountConfirmationMailer<TUser> : IAccountConfirmationMailer
        where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private readonly IEmailSender emailSender;
        private readonly IStringLocalizer<IdentityMessages> localizer;

        public AccountConfirmationMailer(UserManager<TUser> userManager, IEmailSender emailSender,
            IStringLocalizer<IdentityMessages> localizer)
        {
            this.userManager = userManager;
            this.emailSender = emailSender;
            this.localizer = localizer;
        }

        /// <inheritdoc/>
        public async Task<bool> SendConfirmationAsync(string userId, string confirmPageAbsoluteUri, string returnUrl,
            CancellationToken ct = default)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var email = await userManager.GetEmailAsync(user);
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var link = new StringBuilder(confirmPageAbsoluteUri)
                .Append("?userId=").Append(Uri.EscapeDataString(userId))
                .Append("&code=").Append(Uri.EscapeDataString(code));
            if (!string.IsNullOrEmpty(returnUrl))
            {
                link.Append("&returnUrl=").Append(Uri.EscapeDataString(returnUrl));
            }

            await emailSender.SendEmailAsync(email, localizer["Confirm your email"],
                localizer["Please confirm your account by <a href='{0}'>clicking here</a>.",
                    HtmlEncoder.Default.Encode(link.ToString())]);
            return true;
        }
    }
}
