using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;

/// <summary>
/// Shared label resolution for the role-mappings grids: prefer the (optionally multilingual) display name over
/// the raw role name. The display name may be a JSON language record — <see cref="StringExtensions.Translate"/>
/// resolves it for the current UI culture and returns plain text unchanged. Evaluated lazily in the label
/// getters so it picks up the render-time culture.
/// </summary>
internal static class RoleMappingLabels
{
    public static string Localized(string? displayNameJson, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(displayNameJson))
        {
            var translated = displayNameJson.Translate(CultureInfo.CurrentUICulture?.Name);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                return translated;
            }
        }

        return fallback ?? string.Empty;
    }
}

/// <summary>
/// Admin-side view model over a tenant's <c>BillingProfile</c>. Used by the BillingProfiles admin editor
/// (list + edit). The list projection populates the summary fields plus <see cref="EmployeeCount"/> and
/// <see cref="TenantName"/>; the detail projection (used by the edit dialog) additionally fills the
/// addresses. Strategy-agnostic: the flat and hierarchy handlers map their concrete entity onto this shape.
/// </summary>
public class BillingProfileAdminViewModel
{
    public int BillingProfileId { get; set; }

    public int? TenantId { get; set; }

    public string? TenantName { get; set; }

    public ProfileType ProfileType { get; set; } = ProfileType.Personal;

    [MaxLength(256)]
    public string? FirstName { get; set; }

    [MaxLength(256)]
    public string? LastName { get; set; }

    [MaxLength(1024)]
    public string? CompanyName { get; set; }

    [MaxLength(64)]
    public string? VatNumber { get; set; }

    [Required, MaxLength(256), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100), Phone]
    public string? PhoneNumber { get; set; }

    public bool UseInvoiceAddr { get; set; }

    public AddressInput DefaultAddress { get; set; } = new();

    public AddressInput InvoiceAddress { get; set; } = new();

    /// <summary>Number of employee rows on the profile (list display only).</summary>
    public int EmployeeCount { get; set; }

    /// <summary>Display name for the profile (company name or person name), list display only.</summary>
    public string DisplayName =>
        ProfileType == ProfileType.Company
            ? (CompanyName ?? Email)
            : $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// Admin-side view model over an <c>Employee</c> row of a billing profile. <see cref="EMail"/> is the
/// e-mail the invitee is matched on; <see cref="InvitationStatus"/> reflects the join state.
/// </summary>
public class EmployeeViewModel
{
    public int EmployeeId { get; set; }

    public int BillingProfileId { get; set; }

    public int TenantId { get; set; }

    [MaxLength(256)]
    public string? FirstName { get; set; }

    [MaxLength(256)]
    public string? LastName { get; set; }

    [Required, MaxLength(256), EmailAddress]
    public string EMail { get; set; } = string.Empty;

    public InvitationStatus InvitationStatus { get; set; }
}

/// <summary>
/// Row of the employee-role assignment grid: an assignable (DirectRole) employee-role mapping plus whether
/// it is currently assigned to the employee. <see cref="RoleId"/> carries the <c>EmployeeRoleMappingId</c>.
/// </summary>
public class EmployeeRoleAssignmentViewModel
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    /// <summary>Optional multilingual display label as JSON (navigation-label convention).</summary>
    public string? DisplayNameJson { get; set; }

    public bool Assigned { get; set; }

    /// <summary>Effective label for the grid: the localized display name if set, otherwise the role name.</summary>
    public string Label => RoleMappingLabels.Localized(DisplayNameJson, RoleName);
}

/// <summary>
/// Admin view model over an <c>EmployeeRoleMapping</c> — a friendly wrapper around a security role, either a
/// <see cref="EmployeeRoleMappingKind.DirectRole"/> (assignable to employees) or a
/// <see cref="EmployeeRoleMappingKind.PermissionSet"/> (a bundle activated on a DirectRole). On create either
/// link an existing role via <see cref="RoleId"/> or have one created from <see cref="NewRoleName"/>.
/// </summary>
public class EmployeeRoleMappingViewModel
{
    public int EmployeeRoleMappingId { get; set; }

    public EmployeeRoleMappingKind Kind { get; set; } = EmployeeRoleMappingKind.DirectRole;

    /// <summary>Underlying security role. 0 = create a new role from <see cref="NewRoleName"/> on save.</summary>
    public int RoleId { get; set; }

    /// <summary>Underlying role name (read; display only).</summary>
    public string? RoleName { get; set; }

    /// <summary>Name of the role to create when <see cref="RoleId"/> is 0.</summary>
    [MaxLength(150)]
    public string? NewRoleName { get; set; }

    /// <summary>Optional multilingual display label as JSON (navigation-label convention).</summary>
    public string? DisplayNameJson { get; set; }

    /// <summary>Effective label for grids: the localized display name if set, otherwise the role name.</summary>
    public string Label => RoleMappingLabels.Localized(DisplayNameJson, RoleName ?? NewRoleName);
}

/// <summary>A pickable existing tenant security role (for wrapping into a mapping).</summary>
public class TenantRoleOption
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;
}

/// <summary>
/// Row of the "activate permission sets on a DirectRole" grid: a PermissionSet mapping plus whether it is
/// currently activated on the DirectRole (i.e. whether the DirectRole's role inherits the set's role).
/// </summary>
public class PermissionSetActivationViewModel
{
    public int PermissionSetMappingId { get; set; }

    /// <summary>Underlying role name (fallback label).</summary>
    public string? RoleName { get; set; }

    /// <summary>Optional multilingual display label as JSON (navigation-label convention).</summary>
    public string? DisplayNameJson { get; set; }

    public bool Active { get; set; }

    /// <summary>Effective label for the grid: the localized display name if set, otherwise the role name.</summary>
    public string Label => RoleMappingLabels.Localized(DisplayNameJson, RoleName);
}
