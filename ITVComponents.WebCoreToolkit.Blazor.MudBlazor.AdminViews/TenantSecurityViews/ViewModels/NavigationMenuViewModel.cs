using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class NavigationMenuViewModel
{
    public int NavigationMenuId { get; set; }

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Url { get; set; }

    public int? ParentId { get; set; }

    public int? SortOrder { get; set; }

    public int? PermissionId { get; set; }

    public int? FeatureId { get; set; }

    public string? SpanClass { get; set; }

    public bool IsPublic { get; set; }

    public int[] Tenants { get; set; } = Array.Empty<int>();

    public int ChildCount { get; set; }
}

public sealed class NavigationParentChoice
{
    public int NavigationMenuId { get; set; }
    public string DisplayName { get; set; } = "";
}

public sealed class TenantChoice
{
    public int TenantId { get; set; }
    public string DisplayName { get; set; } = "";
}
