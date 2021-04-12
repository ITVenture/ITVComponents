using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
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
