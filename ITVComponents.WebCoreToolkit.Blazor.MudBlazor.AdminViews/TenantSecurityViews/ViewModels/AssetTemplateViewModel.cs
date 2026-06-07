using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class AssetTemplateViewModel
{
    public int AssetTemplateId { get; set; }

    public int? FeatureId { get; set; }

    public int? PermissionId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string SystemKey { get; set; } = string.Empty;
}

public class AssetTemplatePathViewModel
{
    public int AssetTemplatePathId { get; set; }
    public int AssetTemplateId { get; set; }

    [Required, MaxLength(1024)]
    public string PathTemplate { get; set; } = string.Empty;
}

public sealed class AssetTemplatePermissionAssignmentViewModel
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = "";
    public string? Description { get; set; }
    public int AssetTemplateId { get; set; }
    public bool Assigned { get; set; }
}

public sealed class AssetTemplateFeatureAssignmentViewModel
{
    public int FeatureId { get; set; }
    public string FeatureName { get; set; } = "";
    public string? Description { get; set; }
    public int AssetTemplateId { get; set; }
    public bool Assigned { get; set; }
}
