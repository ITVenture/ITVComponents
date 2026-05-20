using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class GlobalRoleViewModel
{
    public int GlobalRoleId { get; set; }

    [Required, MaxLength(200)]
    public string RoleName { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? RoleDescription { get; set; }
}

public sealed class GlobalPermissionAssignmentViewModel
{
    public int GlobalRoleId { get; set; }
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Assigned { get; set; }
}

public sealed class GlobalRoleForLocalRoleAssignmentViewModel
{
    public int GlobalRoleId { get; set; }
    public int LocalRoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool Assigned { get; set; }
}
