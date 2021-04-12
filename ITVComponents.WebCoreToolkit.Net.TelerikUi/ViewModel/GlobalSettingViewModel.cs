using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class GlobalSettingViewModel
    {
        [Key]
        public int GlobalSettingId { get; set; }

        [MaxLength(100),Required]
        public string SettingsKey { get;set; }

        [DataType(DataType.MultilineText)]
        public string SettingsValue { get; set; }

        public bool JsonSetting { get; set; }
    }
}
