using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(TenantTemplateDefinitionMarkup), "base")]
    public class TenantTemplateDefinitionMarkup
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Markup { get; set; }
    }
}
