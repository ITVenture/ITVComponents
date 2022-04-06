using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class SettingTemplateMarkup
    {
        public string ParamName { get; set; }
        
        public bool IsJsonSetting { get; set; }

        public string Value { get; set; }
    }
}
