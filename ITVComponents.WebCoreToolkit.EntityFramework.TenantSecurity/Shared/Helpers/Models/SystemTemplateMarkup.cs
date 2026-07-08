using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataSync;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
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

        public DashboardWidgetLocaleTemplateMarkup[] DashboardWidgetLocales { get; set; }

        public PermissionTemplateMarkup[] Permissions { get; set; }

        public DiagnosticsQueryTemplateMarkup[] DiagnosticsQueries { get; set; }

        public NavigationMenuTemplateMarkup[] Navigation { get; set; }

        public TrustedModuleTemplateMarkup[]  TrustedModules{ get; set; }

        public HealthScriptTemplateMarkup[] HealthScripts { get; set; }
        public GlobalRoleTemplateMarkup[] GlobalRoles { get; set; }
        public AssetTemplateMarkup[] AssetTemplates { get; set; }

        public ExternalOAuthServiceTemplateMarkup[] ExternalOAuthServices { get; set; }

        public TemplateModuleTemplateMarkup[] TemplateModules { get; set; }

        /// <summary>
        /// Additional config sections contributed by feature libraries via <see cref="IConfigExtension"/>
        /// (polymorphic; each entry is a registered <see cref="ConfigExtensionMarkup"/> subtype). Null on older
        /// exports and on systems without any registered extension.
        /// </summary>
        public List<ConfigExtensionMarkup> Extensions { get; set; }
    }
}
