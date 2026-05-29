using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(SystemFeatureTemplateMarkup), "base")]
    public class SystemFeatureTemplateMarkup
    {
        public string FeatureName { get; set; }

        public string FeatureDescription { get; set; }

        public bool Enabled { get; set; }
    }
}
