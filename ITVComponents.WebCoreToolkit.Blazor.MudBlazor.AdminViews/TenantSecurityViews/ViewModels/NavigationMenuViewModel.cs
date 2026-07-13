using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

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

    /// <summary>
    /// Optional free-form metadata as a JSON object (key → value), e.g. <c>{ "HelpSlug": "orders-overview" }</c>.
    /// Surfaced on the runtime navigation model so UI can enrich the entry (context-help button, …).
    /// </summary>
    public string? Metadata { get; set; }

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
