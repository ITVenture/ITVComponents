using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class FeatureViewModel
{
    public int FeatureId { get; set; }

    [Required, MaxLength(512)]
    public string FeatureName { get; set; } = string.Empty;

    public string? FeatureDescription { get; set; }

    public bool Enabled { get; set; }
}
