using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class RoleViewModel
{
    [Key]
    public int RoleId { get; set; }

    [MaxLength(150), Required]
    public string RoleName { get; set; } = "";

    public int TenantId { get; set; }

    public bool IsSystemRole { get; set; }

    public bool Editable { get; set; } = true;
}

public sealed class RoleAssignmentViewModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int TenantId { get; set; }
    public int TenantUserId { get; set; }
    public bool Assigned { get; set; }
    public bool IsSystemRole { get; set; }
}
