using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TenantSettingViewModel
    {
        [Key]
        public int TenantSettingId { get; set; }

        public int TenantId { get; set; }

        [MaxLength(100),Required]
        public string SettingsKey { get;set; }

        [DataType(DataType.MultilineText)]
        public string SettingsValue { get; set; }

        public bool JsonSetting { get; set; }
    }
}
