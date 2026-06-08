using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers;

/// <summary>
/// Tree-only admin surface for the two invitation kinds the unified invite dialog produces:
/// <list type="bullet">
/// <item><description><b>Tenant invitation</b> — invite someone to onboard a brand-new sub-tenant under a
/// parent (tokenized link, drives tenant creation with a pinned parent).</description></item>
/// <item><description><b>Employee invitation</b> — invite a user to join an <i>existing</i> tenant as an
/// employee (e-mail match, accepted via <c>IOnboardingHandler.AcceptInvitationAsync</c>).</description></item>
/// </list>
/// The flat strategy has no equivalent (invitations pin a parent), so this handler is registered only for
/// the hierarchy variant.
/// </summary>
public interface ITenantInvitationHandler
{
    /// <summary>Tenants the calling admin may issue invitations for (their enabled memberships).</summary>
    Task<TenantPickerItem[]> ListAdministrableTenantsAsync(ClaimsPrincipal admin, CancellationToken ct = default);

    // -- Tenant invitations -------------------------------------------------------------------------

    /// <summary>
    /// Creates a tenant invitation under <see cref="TenantInvitationInput.ParentTenantId"/> (the caller must
    /// be an enabled member). Generates a secure token and returns it so the UI can build/send the link.
    /// </summary>
    Task<TenantInvitationResult> CreateTenantInvitationAsync(ClaimsPrincipal admin, TenantInvitationInput input, CancellationToken ct = default);

    /// <summary>Lists tenant invitations issued under the given parent tenant (membership required).</summary>
    Task<TenantInvitationItem[]> ListTenantInvitationsAsync(ClaimsPrincipal admin, int parentTenantId, CancellationToken ct = default);

    /// <summary>Revokes a still-pending tenant invitation (membership in its parent required). Idempotent.</summary>
    Task<bool> RevokeTenantInvitationAsync(ClaimsPrincipal admin, int tenantInvitationId, CancellationToken ct = default);

    /// <summary>
    /// Resolves an invitation token for the accept page (no auth). Flips a past-due pending invitation to
    /// <see cref="InvitationStatus.Expired"/>. Returns null when the token is unknown.
    /// </summary>
    Task<TenantInvitationInfo?> ResolveTenantInvitationAsync(string token, CancellationToken ct = default);

    // -- Employee invitations -----------------------------------------------------------------------

    /// <summary>
    /// Invites a user (by e-mail) to join an existing tenant as employee: creates a pending employee row on
    /// the tenant's billing profile. The invitee accepts via the My-Tenants page. Caller must be a member.
    /// </summary>
    Task<bool> CreateEmployeeInvitationAsync(ClaimsPrincipal admin, EmployeeInvitationInput input, CancellationToken ct = default);

    /// <summary>Lists the employee rows of a tenant (membership required).</summary>
    Task<EmployeeInvitationItem[]> ListEmployeeInvitationsAsync(ClaimsPrincipal admin, int tenantId, CancellationToken ct = default);

    /// <summary>Revokes a still-pending employee invitation (membership in its tenant required). Idempotent.</summary>
    Task<bool> RevokeEmployeeInvitationAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default);
}

/// <summary>Request to create a tenant invitation. <paramref name="LifetimeDays"/> null = default (14 days).</summary>
public record TenantInvitationInput(int ParentTenantId, string Email, int? LifetimeDays = null, string? RoleName = null, string? TemplateName = null);

/// <summary>
/// Outcome of creating a tenant invitation. On success carries the token + expiry so the caller can build
/// the accept link (<c>/Account/Onboarding/Invitation/{Token}</c>) and send the mail.
/// </summary>
public record TenantInvitationResult(bool Succeeded, int TenantInvitationId, string Token, DateTime ExpiresUtc, string? Error);

/// <summary>List projection of a tenant invitation for the admin grid.</summary>
public record TenantInvitationItem(int TenantInvitationId, int ParentTenantId, string Email, string Token,
    DateTime ExpiresUtc, DateTime CreatedUtc, InvitationStatus Status, int? ChildTenantId);

/// <summary>
/// Accept-page projection of a tenant invitation. <paramref name="IsAcceptable"/> is true only when the
/// invitation is pending and not expired.
/// </summary>
public record TenantInvitationInfo(int ParentTenantId, string ParentTenantName, string Email, InvitationStatus Status, bool IsAcceptable);

/// <summary>Request to create an employee invitation on an existing tenant.</summary>
public record EmployeeInvitationInput(int TenantId, string Email, string? FirstName, string? LastName);

/// <summary>List projection of an employee row for the admin grid.</summary>
public record EmployeeInvitationItem(int EmployeeId, int TenantId, string Email, string FirstName, string LastName, InvitationStatus InvitationStatus);
