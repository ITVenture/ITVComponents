using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public sealed class CultureViewModel
{
    public int CultureId { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;
}

public sealed class LocalizationViewModel
{
    public int LocalizationId { get; set; }

    [Required, MaxLength(1024)]
    public string Identifier { get; set; } = string.Empty;
}

public sealed class LocalizationCultureViewModel
{
    public int LocalizationCultureId { get; set; }
    public int LocalizationId { get; set; }
    public int CultureId { get; set; }
    public string? CultureName { get; set; }
}

public sealed class LocalizationStringViewModel
{
    public int LocalizationStringId { get; set; }
    public int LocalizationCultureId { get; set; }

    [Required, MaxLength(256)]
    public string LocalizationKey { get; set; } = string.Empty;

    [Required]
    public string LocalizationValue { get; set; } = string.Empty;
}
