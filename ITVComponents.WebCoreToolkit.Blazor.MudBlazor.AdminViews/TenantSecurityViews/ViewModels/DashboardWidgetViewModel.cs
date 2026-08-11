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

    /// <summary>
    /// Which renderer draws this widget. Empty = the built-in Scriban renderer.
    /// </summary>
    [MaxLength(64)]
    public string? RendererKey { get; set; }

    /// <summary>The renderer's settings as a JSON object (field name -&gt; invariant value).</summary>
    public string? RendererOptions { get; set; }

    /// <summary>
    /// Part of the default collection? A user without own widgets sees exactly these, and gets a copy of
    /// them the moment they start editing their dashboard.
    /// </summary>
    public bool InitiallyActive { get; set; }

    /// <summary>Position within the default collection.</summary>
    public int SortOrder { get; set; }
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
