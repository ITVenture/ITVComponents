using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class TenantTemplateViewModel
{
    public int TenantTemplateId { get; set; }

    [Required, MaxLength(512)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Markup { get; set; }
}
