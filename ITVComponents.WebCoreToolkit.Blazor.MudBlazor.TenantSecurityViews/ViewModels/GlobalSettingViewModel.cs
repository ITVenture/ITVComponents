using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class GlobalSettingViewModel
{
    [Key]
    public int GlobalSettingId { get; set; }

    [MaxLength(100), Required]
    public string SettingsKey { get; set; } = "";

    [DataType(DataType.MultilineText)]
    public string? SettingsValue { get; set; }

    public bool JsonSetting { get; set; }
}
