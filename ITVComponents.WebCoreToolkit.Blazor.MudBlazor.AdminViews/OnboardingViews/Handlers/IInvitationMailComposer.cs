using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers;

/// <summary>
/// Renders and sends the invitation e-mails that the two invitation kinds produce, through the
/// application's configured mail transport (<see cref="ITVComponents.WebCoreToolkit.Security.IAppMailSender"/>,
/// i.e. the same path as the regular identity messages).
/// <para>
/// The message body is taken from a host-configurable template resolved via
/// <see cref="ITVComponents.WebCoreToolkit.Configuration.IGlobalSettingsProvider"/>:
/// <c>{baseKey}.{lang}</c> (culture-specific, e.g. <c>EmpInvitationMessage.de</c>), then <c>{baseKey}</c>
/// (neutral), then a built-in localized default. Base keys are <c>TenantInvitationMessage</c> and
/// <c>EmpInvitationMessage</c>. Templates are raw HTML and may contain
/// <c>ITVComponents.Formatting</c> placeholders <c>[RecipientName]</c>, <c>[TenantName]</c> and <c>[Link]</c>.
/// </para>
/// </summary>
public interface IInvitationMailComposer
{
    /// <summary>
    /// Sends the sub-tenant invitation mail. <paramref name="acceptLink"/> is the absolute token link to the
    /// anonymous accept page. Returns true when the mail was handed to the transport, false when no transport
    /// is configured or sending failed (both are logged and never throw — the invitation itself stays valid).
    /// </summary>
    Task<bool> SendTenantInvitationAsync(string recipientEmail, string? recipientName, string tenantName, string acceptLink, CancellationToken ct = default);

    /// <summary>
    /// Sends the employee invitation mail. Employee invitations carry no token; <paramref name="acceptLink"/>
    /// is the absolute My-Tenants link where the invitee accepts by e-mail match. Same failure semantics as
    /// <see cref="SendTenantInvitationAsync"/>.
    /// </summary>
    Task<bool> SendEmployeeInvitationAsync(string recipientEmail, string? recipientName, string tenantName, string acceptLink, CancellationToken ct = default);
}
