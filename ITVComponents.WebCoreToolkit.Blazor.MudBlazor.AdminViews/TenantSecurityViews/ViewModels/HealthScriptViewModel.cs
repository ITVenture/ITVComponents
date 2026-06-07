using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class HealthScriptViewModel
{
    public int HealthScriptId { get; set; }

    [Required, MaxLength(128)]
    public string HealthScriptName { get; set; } = string.Empty;

    [Required]
    public string Script { get; set; } = string.Empty;
}
