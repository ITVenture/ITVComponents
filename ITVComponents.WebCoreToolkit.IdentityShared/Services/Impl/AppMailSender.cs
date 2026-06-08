using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl
{
    /// <summary>
    /// Default <see cref="IAppMailSender"/>: a thin pass-through to the toolkit's own
    /// <see cref="IEmailSender"/> (DefaultMailSender). Keeps the Identity-UI dependency contained to this
    /// assembly so callers (e.g. the Blazor onboarding views) depend only on the core abstraction.
    /// </summary>
    public class AppMailSender : IAppMailSender
    {
        private readonly IEmailSender emailSender;

        public AppMailSender(IEmailSender emailSender)
        {
            this.emailSender = emailSender;
        }

        /// <inheritdoc/>
        public Task SendMailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => emailSender.SendEmailAsync(toEmail, subject, htmlBody);
    }
}
