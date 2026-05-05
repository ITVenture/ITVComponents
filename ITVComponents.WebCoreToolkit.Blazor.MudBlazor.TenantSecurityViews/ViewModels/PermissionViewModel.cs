using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class PermissionViewModel
{
    [Key]
    public int PermissionId { get; set; }

    [MaxLength(150), Required]
    public string PermissionName { get; set; } = "";

    [MaxLength(2048)]
    public string? Description { get; set; }

    public int? TenantId { get; set; }

    public bool IsGlobal { get; set; }

    public bool Editable { get; set; } = true;
}

public sealed class PermissionAssignmentViewModel
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = "";
    public string? Description { get; set; }
    public int RoleId { get; set; }
    public int TenantId { get; set; }
    public bool IsGlobal { get; set; }
    public bool Assigned { get; set; }
}
