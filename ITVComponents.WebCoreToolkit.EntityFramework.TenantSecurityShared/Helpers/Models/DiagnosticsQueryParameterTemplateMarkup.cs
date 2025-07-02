using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    [JsonDerivedType(typeof(DiagnosticsQueryParameterTemplateMarkup), "base")]
    public class DiagnosticsQueryParameterTemplateMarkup
    {
        public string ParameterName { get; set; }

        public QueryParameterTypes ParameterType { get; set; }

        public string Format { get; set; }

        public bool Optional { get; set; }

        public string DefaultValue { get; set; }
    }
}
