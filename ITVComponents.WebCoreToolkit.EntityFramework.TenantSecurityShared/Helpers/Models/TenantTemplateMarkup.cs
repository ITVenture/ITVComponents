using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class TenantTemplateMarkup
    {
        public RoleTemplateMarkup[] Roles { get; set; }

        public SettingTemplateMarkup[] Settings { get; set; }

        public FeatureTemplateMarkup[] Features { get; set; }

        public PlugInTemplateMarkup[] PlugIns { get; set; }

        public ConstTemplateMarkup[] Constants { get; set; }

        public NavigationTemplateMarkup[] Navigation { get; set; }

        public QueryTemplateMarkup[] Queries { get; set; }
    }
}
