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
        /// <summary>
        /// Name of the export profile that produced this file (<c>Full</c> when none was chosen). Purely a
        /// provenance note for the diff dialog and the download name — what is actually compared is decided by
        /// <see cref="OmitBasicData"/> and by which sections are present.
        /// </summary>
        public string ExportProfile { get; set; }

        /// <summary>
        /// True when the export deliberately left the base system data out (a Help-only or Billing-only export).
        /// A compare then skips the base sections entirely instead of reading their absence as "delete
        /// everything". This is an explicit statement of intent rather than something derived from missing data:
        /// a serializer that writes empty arrays instead of nulls would silently defeat the derived variant, and
        /// the result would be a deletion of the whole system configuration. Absent on older exports, where it
        /// deserializes to false — exactly the previous behaviour.
        /// </summary>
        public bool OmitBasicData { get; set; }

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
