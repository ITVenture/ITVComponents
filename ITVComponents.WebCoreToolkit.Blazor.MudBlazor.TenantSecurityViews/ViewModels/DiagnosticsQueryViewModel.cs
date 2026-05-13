using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class DiagnosticsQueryViewModel
{
    public int DiagnosticsQueryId { get; set; }

    [Required, MaxLength(128)]
    public string DiagnosticsQueryName { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string DbContext { get; set; } = string.Empty;

    public bool AutoReturn { get; set; }

    public string? QueryText { get; set; }

    public int PermissionId { get; set; }

    public int[] Tenants { get; set; } = Array.Empty<int>();
}

public class DiagnosticsQueryParameterViewModel
{
    public int DiagnosticsQueryParameterId { get; set; }
    public int DiagnosticsQueryId { get; set; }

    [Required, MaxLength(128)]
    public string ParameterName { get; set; } = string.Empty;

    public QueryParameterTypes ParameterType { get; set; }

    [MaxLength(64)]
    public string? Format { get; set; }

    public bool Optional { get; set; }

    public string? DefaultValue { get; set; }
}
