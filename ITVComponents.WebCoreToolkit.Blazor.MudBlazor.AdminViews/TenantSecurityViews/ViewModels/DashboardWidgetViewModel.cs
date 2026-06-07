using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class DashboardWidgetViewModel
{
    public int DashboardWidgetId { get; set; }

    [MaxLength(1024)]
    public string? DisplayName { get; set; }

    [MaxLength(2048)]
    public string? TitleTemplate { get; set; }

    [Required, MaxLength(100)]
    public string SystemName { get; set; } = string.Empty;

    public int DiagnosticsQueryId { get; set; }

    public string? Area { get; set; }

    public string? CustomQueryString { get; set; }

    public string? Template { get; set; }
}

public class DashboardParamViewModel
{
    public int DashboardParamId { get; set; }
    public int DashboardWidgetId { get; set; }

    [Required, MaxLength(128)]
    public string ParameterName { get; set; } = string.Empty;

    public InputType InputType { get; set; }

    public string? InputConfig { get; set; }
}

public class DashboardWidgetLocalizationViewModel
{
    public int DashboardWidgetLocalizationId { get; set; }
    public int DashboardWidgetId { get; set; }

    [Required, MaxLength(20)]
    public string LocaleName { get; set; } = string.Empty;

    [Required, MaxLength(1024)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(2048)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Required]
    public string Template { get; set; } = string.Empty;
}
