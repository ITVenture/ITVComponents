using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers;

/// <summary>
/// Strategy-agnostic abstraction over the tenant-onboarding flow. The flat variant is bound
/// to <c>ISecurityContextWithOnboarding</c>; the hierarchy variant adds parent-tenant linkage
/// against <c>IHierarchySecurityContextWithOnboarding</c>. Pages render against this interface
/// and don't know which strategy is active beyond the <see cref="UseHierarchy"/> flag.
/// </summary>
public interface IOnboardingHandler
{
    /// <summary>
    /// True when the active strategy supports a parent-tenant choice. The <c>CreateTenant</c>
    /// page uses this to show/hide the parent-tenant picker.
    /// </summary>
    bool UseHierarchy { get; }

    /// <summary>
    /// Parent-tenant policy for the create-tenant UI in the hierarchy scenario. Derived from the
    /// DB-backed <c>TenantSetupOptions</c> (AllowRootTenantCreation / DefaultParentTenant). The flat
    /// strategy returns a no-op policy (no picker, not required).
    /// </summary>
    OnboardingParentPolicy ParentPolicy { get; }

    /// <summary>
    /// Creates a tenant + billing profile for the calling user. For
    /// <see cref="ProfileType.Personal"/> no employee row is created; for
    /// <see cref="ProfileType.Company"/> the owner is recorded as employee #1.
    /// Returns the new <c>BillingProfileId</c>, or <c>null</c> if creation failed.
    /// </summary>
    Task<int?> CreateTenantAsync(ClaimsPrincipal user, BillingProfileViewModel input, CancellationToken ct = default);

    /// <summary>
    /// Deferred direct onboarding (anonymous): creates the Identity user (unconfirmed) and parks the
    /// requested tenant payload as a pending record keyed by e-mail. No tenant is created yet — that happens
    /// on <see cref="CompletePendingOnboardingAsync"/>. The confirmation mail is sent separately via
    /// <see cref="ITVComponents.WebCoreToolkit.Security.IAccountConfirmationMailer"/> using the returned user id.
    /// </summary>
    Task<OnboardingStartResult> StartOnboardingAsync(OnboardingStartInput input, CancellationToken ct = default);

    /// <summary>
    /// Creates a bare Identity user (unconfirmed) for the employee-invitation join flow — no tenant and no
    /// pending-onboarding payload. The invitee only needs an account to accept an existing tenant's invitation
    /// (matched by e-mail on My-Tenants). The caller sends the confirmation mail separately and may auto-sign-in
    /// on confirmation. Returns the new user id, or the Identity errors on failure.
    /// </summary>
    Task<OnboardingStartResult> RegisterAccountAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// True when an account with the given e-mail exists and has a confirmed e-mail address. Used by the
    /// register-and-wait join page to poll for completion of the e-mail confirmation step.
    /// </summary>
    Task<bool> IsEmailConfirmedAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Parks a join-auto-login nonce on the user (Identity auth-token, provider "Onboarding", name "JoinNonce").
    /// The register-and-wait page also drops the same value as a browser cookie; <c>ConfirmEmail</c> signs the
    /// user in only when both match (same-browser binding). No-op when the e-mail has no account.
    /// </summary>
    Task StoreJoinNonceAsync(string email, string nonce, CancellationToken ct = default);

    /// <summary>
    /// Completes any pending direct-onboarding for the now-authenticated user (matched by e-mail):
    /// creates the tenant from the parked payload and marks the record committed. Idempotent — returns
    /// <c>false</c> when there is nothing pending.
    /// </summary>
    Task<bool> CompletePendingOnboardingAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>
    /// Weist einen selbst registrierten Benutzer dem konfigurierten Standard-Mandanten zu
    /// (<c>TenantSetupOptions.DefaultUserTenant</c>), sofern er noch keinem Mandanten angehoert und keine
    /// Einladung auf ihn wartet. Idempotent; liefert <c>false</c>, wenn nichts zu tun war.
    /// </summary>
    /// <remarks>
    /// Wird erst NACH der Mailbestaetigung gerufen, auf der ersten angemeldeten Landung: einen
    /// unbestaetigten Benutzer einem Mandanten zuzuschlagen hiesse, jemandem Zutritt zu geben, von dem
    /// noch nicht feststeht, dass ihm die Mailadresse ueberhaupt gehoert.
    /// <para>
    /// Eine wartende Einladung hat Vorrang und unterdrueckt die Zuweisung - sie fuehrt den Benutzer
    /// dorthin, wo er tatsaechlich hingehoert, und ihn zusaetzlich in den Standard-Mandanten zu setzen,
    /// waere ungewollter Zutritt.
    /// </para>
    /// </remarks>
    Task<bool> AssignDefaultTenantAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>
    /// Lists all tenants the calling user participates in — both owned tenants and ones where
    /// the user has an open or accepted invitation.
    /// </summary>
    Task<ParticipatingTenantViewModel[]> ListMyTenantsAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>
    /// Accepts a pending invitation for the given billing profile. Idempotent: returns
    /// <c>false</c> if no pending invitation exists for this user.
    /// </summary>
    Task<bool> AcceptInvitationAsync(ClaimsPrincipal user, int billingProfileId, CancellationToken ct = default);

    /// <summary>
    /// Returns the tenants the user may pick as a parent for a new sub-tenant. The flat
    /// strategy returns an empty array. The hierarchy strategy returns the tenants the user
    /// is currently a member of.
    /// </summary>
    Task<TenantPickerItem[]> ListEligibleParentsAsync(ClaimsPrincipal user, CancellationToken ct = default);
}

/// <summary>
/// Compact tenant projection used to populate parent-tenant pickers in the create-tenant flow.
/// </summary>
public record TenantPickerItem(int TenantId, string DisplayName);

/// <summary>
/// Drives the parent-tenant picker in the create-tenant page. <paramref name="ShowPicker"/> is false
/// when a default parent is forced (auto-assigned) or the flat strategy is active.
/// <paramref name="ParentRequired"/> is true when root tenants are disallowed and no default parent
/// exists, i.e. the user must pick a parent.
/// </summary>
public record OnboardingParentPolicy(bool ShowPicker, bool ParentRequired);

/// <summary>
/// Input for <see cref="IOnboardingHandler.StartOnboardingAsync"/>: the account credentials plus the
/// tenant/billing-profile request that is parked until the user confirms their e-mail. An optional
/// tenant-invitation token can pin the future parent tenant (tree flow).
/// </summary>
public record OnboardingStartInput(string Email, string Password, BillingProfileViewModel Profile, string? InvitationToken = null);

/// <summary>
/// Result of <see cref="IOnboardingHandler.StartOnboardingAsync"/>. On success carries the new user id
/// (used to trigger the confirmation mail); on failure carries the Identity error descriptions.
/// </summary>
public record OnboardingStartResult(bool Succeeded, string? UserId, IReadOnlyList<string> Errors);
