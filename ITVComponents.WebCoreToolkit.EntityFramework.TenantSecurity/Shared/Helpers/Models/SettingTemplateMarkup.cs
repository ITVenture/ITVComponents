using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(SettingTemplateMarkup), "base")]
    public class SettingTemplateMarkup
    {
        public string ParamName { get; set; }
        
        public bool IsJsonSetting { get; set; }

        public string Value { get; set; }
    }
}
