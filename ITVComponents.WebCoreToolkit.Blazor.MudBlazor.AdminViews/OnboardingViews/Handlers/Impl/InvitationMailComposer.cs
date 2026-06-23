using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Default <see cref="IInvitationMailComposer"/>: resolves the body template from global settings (with a
/// built-in localized fallback), substitutes the placeholders via <c>ITVComponents.Formatting</c> and sends
/// the resulting HTML mail through <see cref="IAppMailSender"/>. The template language is HTML — the whole
/// mail pipeline (<c>DefaultMailSender</c>) is HTML-native — and the formatter's <c>[...]</c> placeholder
/// syntax sits cleanly in HTML (Markdown's <c>[text](url)</c> would collide with it).
/// </summary>
public class InvitationMailComposer : IInvitationMailComposer
{
    /// <summary>Global-settings base key for the sub-tenant invitation body (suffix with .{lang} to localize).</summary>
    private const string TenantSettingKey = "TenantInvitationMessage";

    /// <summary>Global-settings base key for the employee invitation body (suffix with .{lang} to localize).</summary>
    private const string EmployeeSettingKey = "EmpInvitationMessage";

    private readonly IAppMailSender mailSender;
    private readonly IStringLocalizer<OnboardingMessages> localizer;
    private readonly IServiceProvider services;
    private readonly ILogger<InvitationMailComposer> logger;

    public InvitationMailComposer(IAppMailSender mailSender, IStringLocalizer<OnboardingMessages> localizer,
        IServiceProvider services, ILogger<InvitationMailComposer> logger)
    {
        this.mailSender = mailSender;
        this.localizer = localizer;
        this.services = services;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> SendTenantInvitationAsync(string recipientEmail, string? recipientName, string tenantName, string acceptLink, CancellationToken ct = default)
        => SendAsync(TenantSettingKey, "Onb_Inv_MailSubject", "Onb_Inv_TenantMailTemplate", recipientEmail, recipientName, tenantName, acceptLink, ct);

    /// <inheritdoc/>
    public Task<bool> SendEmployeeInvitationAsync(string recipientEmail, string? recipientName, string tenantName, string acceptLink, CancellationToken ct = default)
        => SendAsync(EmployeeSettingKey, "Onb_Inv_EmployeeMailSubject", "Onb_Inv_EmployeeMailTemplate", recipientEmail, recipientName, tenantName, acceptLink, ct);

    private async Task<bool> SendAsync(string settingBaseKey, string subjectResourceKey, string defaultBodyResourceKey,
        string recipientEmail, string? recipientName, string tenantName, string acceptLink, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return false;
        }

        // Values are HTML-encoded before they enter the HTML template, so a tenant/recipient name can never
        // break the markup or inject content. The formatter only scans the *template* for [..]; the substituted
        // values are inserted verbatim and not re-parsed, so brackets inside a name are harmless.
        var enc = HtmlEncoder.Default;
        var model = new
        {
            RecipientName = enc.Encode(string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName!),
            TenantName = enc.Encode(tenantName ?? ""),
            Link = enc.Encode(acceptLink ?? "")
        };

        var subjectTemplate = localizer[subjectResourceKey].Value;
        var bodyTemplate = ResolveBodyTemplate(settingBaseKey, defaultBodyResourceKey);
        string subject, body;
        try
        {
            subject = model.FormatText(subjectTemplate);
            body = model.FormatText(bodyTemplate);
        }
        catch (Exception ex)
        {
            // A malformed (admin-authored) template must not break invitations: fall back to the built-in default.
            logger.LogWarning(ex, "Invitation mail template '{Key}' is malformed; using the built-in default.", settingBaseKey);
            subject = localizer[subjectResourceKey].Value;
            body = model.FormatText(localizer[defaultBodyResourceKey].Value);
        }

        try
        {
            await mailSender.SendMailAsync(recipientEmail, subject, body, ct);
            return true;
        }
        catch (Exception ex)
        {
            // No SMTP configured / transport error must not fail the invitation — the row is already persisted.
            logger.LogWarning(ex, "Could not send invitation mail to {Recipient}.", recipientEmail);
            return false;
        }
    }

    /// <summary>
    /// Resolves the body template: culture-specific global setting (<c>{key}.{lang}</c>), then the neutral
    /// global setting (<c>{key}</c>), then the built-in localized default resource. The settings provider is
    /// optional — without it the built-in default is used, so invitations work out of the box.
    /// </summary>
    private string ResolveBodyTemplate(string settingBaseKey, string defaultBodyResourceKey)
    {
        var provider = services.GetService<IGlobalSettingsProvider>();
        if (provider != null)
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var localized = provider.GetLiteralSetting($"{settingBaseKey}.{lang}");
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }

            var neutral = provider.GetLiteralSetting(settingBaseKey);
            if (!string.IsNullOrWhiteSpace(neutral))
            {
                return neutral;
            }
        }

        return localizer[defaultBodyResourceKey].Value;
    }
}
