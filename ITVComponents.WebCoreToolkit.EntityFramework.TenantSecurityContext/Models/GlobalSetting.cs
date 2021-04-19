using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    [Index(nameof(SettingsKey), IsUnique=true, Name="UQ_GlobalSettingsKey")]
    public class GlobalSetting
    {
        [Key]
        public int GlobalSettingId { get; set; }

        [MaxLength(100),Required]
        public string SettingsKey { get;set; }

        public string SettingsValue { get; set; }

        public bool JsonSetting { get; set; }
    }
}
