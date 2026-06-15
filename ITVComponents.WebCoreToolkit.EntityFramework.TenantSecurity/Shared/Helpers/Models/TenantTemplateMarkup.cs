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
        /// <c>ITenantTemplatePartHandler</c> (key = part key, value = the handler's serialized payload). Kept as
        /// plain string→string so the engine never needs to know the concrete part types.
        /// </summary>
        public Dictionary<string, string> Extensions { get; set; }
    }
}
