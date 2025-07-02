using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(AssetTemplateMarkup), "base")]
    public class AssetTemplateMarkup
    {
        public string RequiredFeature { get; set; }

        public string RequiredPermission { get; set; }
        public string Name { get; set; }
        public string SystemKey { get; set; }

        public string[] PathTemplates { get; set; }

        public string[] Grants { get; set; }

        public string[] FeatureGrants { get; set; }
    }
}
