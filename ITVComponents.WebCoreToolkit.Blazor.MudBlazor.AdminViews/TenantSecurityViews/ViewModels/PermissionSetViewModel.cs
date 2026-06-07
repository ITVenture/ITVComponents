using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class PermissionSetViewModel
{
    public int AppPermissionSetId { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}

public sealed class AppPermissionAssignmentViewModel
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = "";
    public string? Description { get; set; }
    public int AppPermissionSetId { get; set; }
    public bool Assigned { get; set; }
}
