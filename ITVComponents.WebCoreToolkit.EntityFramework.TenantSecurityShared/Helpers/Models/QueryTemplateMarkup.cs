using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(QueryTemplateMarkup), "base")]
    public class QueryTemplateMarkup
    {
        public string Name { get; set; }
    }
}
