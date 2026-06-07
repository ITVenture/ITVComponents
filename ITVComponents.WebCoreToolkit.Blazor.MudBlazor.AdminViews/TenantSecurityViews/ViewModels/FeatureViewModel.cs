using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class FeatureViewModel
{
    public int FeatureId { get; set; }

    [Required, MaxLength(512)]
    public string FeatureName { get; set; } = string.Empty;

    public string? FeatureDescription { get; set; }

    public bool Enabled { get; set; }
}

public sealed class FeatureActivationViewModel
{
    public int TenantFeatureActivationId { get; set; }
    public int FeatureId { get; set; }
    public int TenantId { get; set; }
    public string? TenantDisplayName { get; set; }
    public DateTime? ActivationStart { get; set; }
    public DateTime? ActivationEnd { get; set; }
}

public sealed class TemplateModuleViewModel
{
    public int TemplateModuleId { get; set; }

    [Required, MaxLength(255)]
    public string TemplateModuleName { get; set; } = string.Empty;

    public int FeatureId { get; set; }
}

public sealed class TemplateModuleConfiguratorViewModel
{
    public int TemplateModuleConfiguratorId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? CustomConfiguratorView { get; set; }

    [Required, MaxLength(2048)]
    public string ConfiguratorTypeBack { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public int TemplateModuleId { get; set; }
}

public sealed class TemplateModuleConfiguratorParameterViewModel
{
    public int TemplateModuleCfgParameterId { get; set; }

    [Required, MaxLength(1024)]
    public string ParameterName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    [Required]
    public string ParameterValue { get; set; } = string.Empty;

    public int TemplateModuleConfiguratorId { get; set; }
}

public sealed class TemplateModuleScriptViewModel
{
    public int TemplateModuleScriptId { get; set; }

    [Required, MaxLength(1024)]
    public string ScriptFile { get; set; } = string.Empty;

    public int TemplateModuleId { get; set; }
}
