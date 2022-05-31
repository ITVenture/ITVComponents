using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class SystemTemplateMarkup
    {
        public SettingTemplateMarkup[] Settings { get; set; }
        
        public SystemFeatureTemplateMarkup[] Features { get; set; }

        public AuthenticationTypeTemplateMarkup[] AuthenticationTypes { get; set; }

        public AuthenticationTypeClaimTemplateMarkup[] AuthenticationTypeClaimTemplates { get; set; }

        public TenantTemplateDefinitionMarkup[] TenantTemplates { get; set; }

        public PlugInTemplateMarkup[] PlugIns { get; set; }

        public ConstTemplateMarkup[] Constants { get; set; }

        public DashboardWidgetTemplateMarkup[] DashboardWidgets { get; set; }

        public PermissionTemplateMarkup[] Permissions { get; set; }

        public DiagnosticsQueryTemplateMarkup[] DiagnosticsQueries { get; set; }

        public NavigationMenuTemplateMarkup[] Navigation { get; set; }

        public TrustedModuleTemplateMarkup[]  TrustedModules{ get; set; }
    }
}
