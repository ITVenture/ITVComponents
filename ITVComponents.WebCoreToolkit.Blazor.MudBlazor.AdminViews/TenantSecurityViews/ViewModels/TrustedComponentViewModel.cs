using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class TrustedComponentViewModel
{
    public int TrustedFullAccessComponentId { get; set; }

    [Required, MaxLength(1024)]
    public string FullQualifiedTypeName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? TargetQualifiedTypeName { get; set; }

    public string? Description { get; set; }

    public string? TrustLevelConfig { get; set; }
}
