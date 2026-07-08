using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class TenantViewModel
{
    [Key]
    public int TenantId { get; set; }

    [Required, MaxLength(150)]
    public string TenantName { get; set; } = "";

    [MaxLength(1024)]
    public string? DisplayName { get; set; }

    public string? TimeZone { get; set; }

    public int? TenantTypeId { get; set; }

    public int? ParentTenantId { get; set; }

    /// <summary>
    /// True when the tenant has a <c>TenantType</c> assigned whose <c>TenantTemplate</c> is set — i.e. the tenant's
    /// type template can be (re-)applied. Drives the visibility of the re-apply action in the tenants grid.
    /// </summary>
    public bool CanReapplyTemplate { get; set; }
}

public sealed class TenantAssignmentViewModel
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = "";
    public string? DisplayName { get; set; }
    public bool Assigned { get; set; }
}

public sealed class TenantSettingViewModel
{
    public int TenantSettingId { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string SettingsKey { get; set; } = string.Empty;

    [Required]
    public string SettingsValue { get; set; } = string.Empty;

    public bool JsonSetting { get; set; }
}

public sealed class TenantFeatureActivationAssignmentViewModel
{
    public int TenantId { get; set; }
    public int FeatureId { get; set; }
    public string FeatureName { get; set; } = string.Empty;
    public bool Assigned { get; set; }
    public int? TenantFeatureActivationId { get; set; }
    public DateTime? ActivationStart { get; set; }
    public DateTime? ActivationEnd { get; set; }
}

public sealed class TenantNavigationAssignmentViewModel
{
    public int TenantId { get; set; }
    public int NavigationMenuId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool Assigned { get; set; }
}
