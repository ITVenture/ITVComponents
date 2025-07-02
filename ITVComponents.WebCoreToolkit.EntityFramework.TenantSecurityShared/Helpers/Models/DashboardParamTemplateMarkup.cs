using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(DashboardParamTemplateMarkup), "base")]
    public class DashboardParamTemplateMarkup
    {
        public string ParameterName { get; set; }

        public InputType InputType { get; set; }

        public string InputConfig { get; set; }
    }
}
