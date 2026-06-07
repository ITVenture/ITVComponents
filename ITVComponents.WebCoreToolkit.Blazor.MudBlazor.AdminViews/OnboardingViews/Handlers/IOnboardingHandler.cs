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
