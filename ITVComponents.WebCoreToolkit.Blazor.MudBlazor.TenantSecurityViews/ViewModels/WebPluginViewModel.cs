using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public sealed class WebPluginViewModel
{
    public int WebPluginId { get; set; }
    public int? TenantId { get; set; }

    [Required, MaxLength(300)]
    public string UniqueName { get; set; } = string.Empty;

    [MaxLength(8192)]
    public string? Constructor { get; set; }

    public bool AutoLoad { get; set; }
    public bool Transient { get; set; }

    [MaxLength(8192)]
    public string? StartupRegistrationConstructor { get; set; }
}

public sealed class WebPluginConstantViewModel
{
    public int WebPluginConstantId { get; set; }
    public int? TenantId { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}

public sealed class WebPluginGenericParameterViewModel
{
    public int WebPluginGenericParameterId { get; set; }
    public int WebPluginId { get; set; }

    [MaxLength(200)]
    public string? GenericTypeName { get; set; }

    [MaxLength(2048)]
    public string? TypeExpression { get; set; }
}
