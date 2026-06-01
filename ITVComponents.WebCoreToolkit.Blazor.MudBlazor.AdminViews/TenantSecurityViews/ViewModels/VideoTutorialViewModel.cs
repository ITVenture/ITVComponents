using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public sealed class VideoTutorialViewModel
{
    public int VideoTutorialId { get; set; }

    [Required, MaxLength(200)]
    public string SortableName { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? ModuleUrl { get; set; }
}

public sealed class TutorialStreamViewModel
{
    public int TutorialStreamId { get; set; }
    public int VideoTutorialId { get; set; }

    [Required, MaxLength(100)]
    public string LanguageTag { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
}
