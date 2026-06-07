using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class TenantTypeViewModel
{
    public int TenantTypeId { get; set; }

    [Required, MaxLength(512)]
    public string TenantTypeName { get; set; } = string.Empty;

    public string? TypeMetaData { get; set; }

    public int? TenantTemplateId { get; set; }
}
