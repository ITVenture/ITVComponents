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
/// Permission names that gate the onboarding admin editors. A single <see cref="ManageEmployees"/>
/// permission grants read + write over billing profiles, employees and their role assignments, checked
/// against the ambient permission-scope (the current tenant) both in the UI and, authoritatively, in the
/// handler.
/// </summary>
public static class OnboardingAdminPermissions
{
    public const string ManageEmployees = "ManageEmployees";
}
