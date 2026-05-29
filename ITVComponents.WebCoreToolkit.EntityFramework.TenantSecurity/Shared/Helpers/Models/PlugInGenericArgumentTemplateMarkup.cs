using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(PlugInGenericArgumentTemplateMarkup), "base")]
    public class PlugInGenericArgumentTemplateMarkup
    {
        public string GenericTypeName { get; set; }
        public string TypeExpression { get; set; }
    }
}
