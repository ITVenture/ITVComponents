using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers;

/// <summary>
/// Admin surface for maintaining a tenant's onboarding master data — the <c>BillingProfile</c>, its
/// <c>Employee</c> rows and the security roles assigned to each employee (<c>EmployeeRole</c>). Both the
/// flat and hierarchy strategies implement this against their respective onboarding context
/// (<c>ISecurityContextWithOnboarding</c> / <c>IHierarchySecurityContextWithOnboarding</c>); the editor
/// pages render against this interface and don't know which strategy is active.
/// <para>
/// Every operation works over the caller's <b>current scope tenant</b> (<c>CurrentTenantId</c>) and is
/// gated, authoritatively in the handler, by the <see cref="OnboardingAdminPermissions.ManageEmployees"/>
/// permission plus an enabled membership in that tenant — the same gate the UI's SecureView enforces.
/// </para>
/// </summary>
public interface IOnboardingAdminHandler
{
    /// <summary>True when the ambient user holds ANY of <paramref name="permissions"/> in the current scope.</summary>
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    /// <summary>True when the current user may manage employees/profiles (holds ManageEmployees).</summary>
    bool CanManage(ClaimsPrincipal user);

    /// <summary>
    /// True when the current user holds <see cref="OnboardingAdminPermissions.RoleMappingsAllFeatures"/> — i.e. may
    /// assign a feature gate to a role mapping and see mappings gated to features the tenant lacks. Drives whether
    /// the feature dropdown is shown and whether the visibility filter is bypassed.
    /// </summary>
    bool CanManageAllFeatures(ClaimsPrincipal user);

    /// <summary>
    /// True when new role-mappings must always create a dedicated new role (the "wrap an existing role" option is
    /// disabled), per <c>TenantSetupOptions.ForceDedicatedRoleForMappings</c>. The dialog hides the role picker
    /// accordingly; the handler enforces it on save regardless of the UI.
    /// </summary>
    bool ForceDedicatedRoleForMappings { get; }

    /// <summary>
    /// The tenant the editor operates on: the caller's currently selected scope tenant. Returns null when
    /// there is no current tenant scope or the caller is not an enabled member of it.
    /// </summary>
    Task<TenantPickerItem?> GetCurrentTenantAsync(ClaimsPrincipal admin, CancellationToken ct = default);

    // -- Billing profiles ---------------------------------------------------------------------------

    /// <summary>Lists the billing profiles of the current scope tenant (summary projection).</summary>
    Task<BillingProfileAdminViewModel[]> ListBillingProfilesAsync(ClaimsPrincipal admin, CancellationToken ct = default);

    /// <summary>Loads a single billing profile (including addresses) for editing; null if not in scope.</summary>
    Task<BillingProfileAdminViewModel?> GetBillingProfileAsync(ClaimsPrincipal admin, int billingProfileId, CancellationToken ct = default);

    /// <summary>
    /// Creates (BillingProfileId == 0) or updates a billing profile in the current scope tenant. Returns the
    /// profile id, or null when not authorized / out of scope.
    /// </summary>
    Task<int?> SaveBillingProfileAsync(ClaimsPrincipal admin, BillingProfileAdminViewModel model, CancellationToken ct = default);

    // -- Employees ----------------------------------------------------------------------------------

    /// <summary>Lists the employee rows of a billing profile in the current scope tenant.</summary>
    Task<EmployeeViewModel[]> ListEmployeesAsync(ClaimsPrincipal admin, int billingProfileId, CancellationToken ct = default);

    /// <summary>
    /// Creates (EmployeeId == 0) or updates an employee on a billing profile of the current scope tenant.
    /// Returns the employee id, or null when not authorized / out of scope.
    /// </summary>
    Task<int?> SaveEmployeeAsync(ClaimsPrincipal admin, EmployeeViewModel model, CancellationToken ct = default);

    /// <summary>Deletes an employee row of the current scope tenant. Idempotent; false when out of scope.</summary>
    Task<bool> DeleteEmployeeAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default);

    // -- Employee roles -----------------------------------------------------------------------------

    /// <summary>
    /// Lists the assignable (DirectRole) employee-role mappings of the current scope tenant, each flagged with
    /// whether it is currently assigned to the given employee. (<c>RoleId</c> on the row carries the
    /// EmployeeRoleMappingId.)
    /// </summary>
    Task<EmployeeRoleAssignmentViewModel[]> ListEmployeeRolesAsync(ClaimsPrincipal admin, int employeeId, CancellationToken ct = default);

    /// <summary>
    /// Assigns (or removes) a DirectRole employee-role mapping to/from an employee of the current scope tenant.
    /// The underlying user gets the mapping's role materialized by the onboarding interceptor. Idempotent;
    /// false when not authorized / out of scope.
    /// </summary>
    Task<bool> SetEmployeeRoleAsync(ClaimsPrincipal admin, int employeeId, int employeeRoleMappingId, bool assigned, CancellationToken ct = default);

    // -- Employee-role mappings (catalog) -----------------------------------------------------------

    /// <summary>Lists all employee-role mappings (DirectRole + PermissionSet) of the current scope tenant.</summary>
    Task<EmployeeRoleMappingViewModel[]> ListRoleMappingsAsync(ClaimsPrincipal admin, CancellationToken ct = default);

    /// <summary>Existing tenant security roles, for wrapping into a mapping.</summary>
    Task<TenantRoleOption[]> ListTenantRolesAsync(ClaimsPrincipal admin, CancellationToken ct = default);

    /// <summary>
    /// The feature catalog (name + description + enabled), for the mapping's optional visibility-gate dropdown.
    /// Only meaningful for callers holding <see cref="OnboardingAdminPermissions.RoleMappingsAllFeatures"/>;
    /// returns empty for everyone else.
    /// </summary>
    Task<FeatureOption[]> ListFeaturesAsync(ClaimsPrincipal admin, CancellationToken ct = default);

    /// <summary>
    /// Creates (EmployeeRoleMappingId == 0) or updates an employee-role mapping in the current scope tenant.
    /// On create, links <c>RoleId</c> when set, otherwise creates a new role from <c>NewRoleName</c>. Returns
    /// the mapping id, or null when not authorized / out of scope / invalid.
    /// </summary>
    Task<int?> SaveRoleMappingAsync(ClaimsPrincipal admin, EmployeeRoleMappingViewModel model, CancellationToken ct = default);

    /// <summary>Deletes a mapping of the current scope tenant (plus its employee assignments). Idempotent.</summary>
    Task<bool> DeleteRoleMappingAsync(ClaimsPrincipal admin, int employeeRoleMappingId, CancellationToken ct = default);

    /// <summary>
    /// For a DirectRole mapping, lists the PermissionSet mappings of the tenant, each flagged with whether it is
    /// currently activated on the DirectRole (role inheritance present).
    /// </summary>
    Task<PermissionSetActivationViewModel[]> ListActivatablePermissionSetsAsync(ClaimsPrincipal admin, int directMappingId, CancellationToken ct = default);

    /// <summary>
    /// Activates (or deactivates) a PermissionSet mapping on a DirectRole mapping by adding/removing the role
    /// inheritance (RoleRole) between their underlying roles. Idempotent; false when not authorized / invalid.
    /// </summary>
    Task<bool> SetPermissionSetActivationAsync(ClaimsPrincipal admin, int directMappingId, int permissionSetMappingId, bool active, CancellationToken ct = default);
}

/// <summary>
/// Permission names that gate the onboarding admin surface (the tabbed BillingProfile view). Per area a
/// <c>.View</c> (read) and <c>.Write</c> (read + write) permission under the <c>Onboarding.Admin.*</c>
/// prefix; <c>.Write</c> implies <c>.View</c> (read access = holding either). For role mappings the kind is
/// gated additionally by <c>.DirectRole</c> / <c>.PermissionSet</c> so a tenant can be limited to one kind.
/// All checks run against the ambient permission-scope (the current tenant), both in the UI (SecureView) and,
/// authoritatively, in the handler.
/// </summary>
public static class OnboardingAdminPermissions
{
    public const string BillingProfileView = "Onboarding.Admin.BillingProfile.View";
    public const string BillingProfileWrite = "Onboarding.Admin.BillingProfile.Write";
    public const string EmployeesView = "Onboarding.Admin.Employees.View";
    public const string EmployeesWrite = "Onboarding.Admin.Employees.Write";
    public const string RoleMappingsView = "Onboarding.Admin.RoleMappings.View";
    public const string RoleMappingsWrite = "Onboarding.Admin.RoleMappings.Write";
    public const string RoleMappingsDirectRole = "Onboarding.Admin.RoleMappings.DirectRole";
    public const string RoleMappingsPermissionSet = "Onboarding.Admin.RoleMappings.PermissionSet";

    /// <summary>Full edit of a Delegation mapping (create/edit/delete/rename), analogous to the other kind-permissions.</summary>
    public const string RoleMappingsDelegation = "Onboarding.Admin.RoleMappings.Delegation";

    /// <summary>Weaker "delegate" tier: see a Delegation role and activate/deactivate its PermissionSets only — no create/edit/delete/rename.</summary>
    public const string RoleMappingsDelegationAssign = "Onboarding.Admin.RoleMappings.DelegationAssign";

    /// <summary>
    /// Override that lifts the per-tenant feature gate on role mappings: the holder sees mappings gated to a
    /// feature the tenant has not subscribed, and is the only one allowed to assign a feature gate when creating
    /// a mapping. Users without it only see neutral (ungated) or currently-entitled mappings, and can only
    /// create neutral ones. This is a modifier — it grants no access on its own; a normal RoleMappings read/write
    /// permission is still required to reach the tab.
    /// </summary>
    public const string RoleMappingsAllFeatures = "Onboarding.Admin.RoleMappings.AllFeatures";
    public const string SubTenantsView = "Onboarding.Admin.SubTenants.View";
    public const string SubTenantsWrite = "Onboarding.Admin.SubTenants.Write";

    /// <summary>Read access to the billing-profile tab (View or Write).</summary>
    public static readonly string[] BillingProfileRead = { BillingProfileView, BillingProfileWrite };

    /// <summary>Read access to the employees tab (View or Write).</summary>
    public static readonly string[] EmployeesRead = { EmployeesView, EmployeesWrite };

    /// <summary>Read access to the role-mappings tab (any of its permissions).</summary>
    public static readonly string[] RoleMappingsRead = { RoleMappingsView, RoleMappingsWrite, RoleMappingsDirectRole, RoleMappingsPermissionSet, RoleMappingsDelegation, RoleMappingsDelegationAssign };

    /// <summary>Write a DirectRole mapping: the DirectRole kind-permission or the generic role-mappings write.</summary>
    public static readonly string[] DirectRoleWrite = { RoleMappingsDirectRole, RoleMappingsWrite };

    /// <summary>Write a PermissionSet mapping: the PermissionSet kind-permission or the generic role-mappings write.</summary>
    public static readonly string[] PermissionSetWrite = { RoleMappingsPermissionSet, RoleMappingsWrite };

    /// <summary>Full edit of a Delegation mapping (create/edit/delete/rename): the Delegation kind-permission or the generic write.</summary>
    public static readonly string[] DelegationWrite = { RoleMappingsDelegation, RoleMappingsWrite };

    /// <summary>
    /// May see a Delegation role and assign PermissionSets to it: either the full-edit tier (Delegation kind /
    /// generic write) or the weaker delegate tier (<see cref="RoleMappingsDelegationAssign"/>).
    /// </summary>
    public static readonly string[] DelegationAssign = { RoleMappingsDelegation, RoleMappingsWrite, RoleMappingsDelegationAssign };

    /// <summary>Any write within role mappings (used to gate delete before the row's kind is known).</summary>
    public static readonly string[] RoleMappingsAnyWrite = { RoleMappingsDirectRole, RoleMappingsPermissionSet, RoleMappingsDelegation, RoleMappingsWrite };

    /// <summary>The feature-gate override (see <see cref="RoleMappingsAllFeatures"/>), in array form for the permission check.</summary>
    public static readonly string[] AllFeatures = { RoleMappingsAllFeatures };

    /// <summary>Read access to the sub-tenant invitations tab (View or Write).</summary>
    public static readonly string[] SubTenantsRead = { SubTenantsView, SubTenantsWrite };

    /// <summary>Any onboarding-admin permission — grants access to the surface (e.g. GetCurrentTenant).</summary>
    public static readonly string[] AnyAccess =
    {
        BillingProfileView, BillingProfileWrite, EmployeesView, EmployeesWrite,
        RoleMappingsView, RoleMappingsWrite, RoleMappingsDirectRole, RoleMappingsPermissionSet,
        RoleMappingsDelegation, RoleMappingsDelegationAssign,
        SubTenantsView, SubTenantsWrite
    };
}
