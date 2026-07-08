using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    public class TenantTemplateMarkup
    {
        public PermissionTemplateMarkup[] ExplicitPermissions { get; set; }
        public RoleTemplateMarkup[] Roles { get; set; }

        public SettingTemplateMarkup[] Settings { get; set; }

        public FeatureTemplateMarkup[] Features { get; set; }

        public PlugInTemplateMarkup[] PlugIns { get; set; }

        public ConstTemplateMarkup[] Constants { get; set; }

        public NavigationTemplateMarkup[] Navigation { get; set; }

        public QueryTemplateMarkup[] Queries { get; set; }

        public ExternalOAuthServiceTemplateMarkup[] ExternalOAuthServices { get; set; }

        /// <summary>
        /// Open extension bag for decoupled template parts contributed by feature libraries via
        /// <c>ITenantTemplatePartHandler</c> (key = part key, value = the handler's payload + its apply mode).
        /// </summary>
        public Dictionary<string, TemplateExtensionMarkup> Extensions { get; set; }

        /// <summary>
        /// Per-kind apply mode. <see cref="TemplateApplyMode.Auto"/> (the default) inherits the applying method's
        /// mode; set <see cref="TemplateApplyMode.Additive"/> to keep the tenant's own extra entries of that kind, or
        /// <see cref="TemplateApplyMode.Forced"/> to prune them so the tenant matches the template exactly.
        /// </summary>
        public TemplateApplyMode ApplyModeForPermissions { get; set; }

        public TemplateApplyMode ApplyModeForRoles { get; set; }

        public TemplateApplyMode ApplyModeForSettings { get; set; }

        public TemplateApplyMode ApplyModeForFeatures { get; set; }

        public TemplateApplyMode ApplyModeForPlugIns { get; set; }

        public TemplateApplyMode ApplyModeForConstants { get; set; }

        public TemplateApplyMode ApplyModeForNavigation { get; set; }

        public TemplateApplyMode ApplyModeForQueries { get; set; }

        public TemplateApplyMode ApplyModeForExternalOAuthServices { get; set; }
    }
}
