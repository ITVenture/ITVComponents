using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(HealthScriptTemplateMarkup), "base")]
    public class HealthScriptTemplateMarkup
    {
        public string HealthScriptName { get; set; }

        public string Script { get; set; }
    }
}
