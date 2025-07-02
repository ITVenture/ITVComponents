using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(TrustedModuleTemplateMarkup), "base")]
    public class TrustedModuleTemplateMarkup
    {
        public string FullQualifiedTypeName { get; set; }
        public string Description { get; set; }
        public string TargetQualifiedTypeName { get; set; }
        public string TrustLevelConfig { get; set; }
    }
}
