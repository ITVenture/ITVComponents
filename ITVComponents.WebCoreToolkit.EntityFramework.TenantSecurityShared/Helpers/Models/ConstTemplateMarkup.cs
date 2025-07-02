using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(ConstTemplateMarkup), "base")]
    public class ConstTemplateMarkup
    {
        public string Name { get; set; }

        public string Value { get; set; }
    }
}
