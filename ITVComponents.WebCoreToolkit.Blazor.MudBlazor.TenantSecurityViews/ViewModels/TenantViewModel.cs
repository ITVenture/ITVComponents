using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

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
}

public sealed class TenantAssignmentViewModel
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = "";
    public string? DisplayName { get; set; }
    public bool Assigned { get; set; }
}
