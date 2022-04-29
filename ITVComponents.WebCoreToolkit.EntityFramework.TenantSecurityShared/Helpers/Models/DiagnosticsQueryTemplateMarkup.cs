using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class DiagnosticsQueryTemplateMarkup
    {
        public string DiagnosticsQueryName { get; set; }

        public string DbContext { get; set; }

        public bool AutoReturn { get; set; }

        public string QueryText { get; set; }

        public string Permission { get; set; }

        public DiagnosticsQueryParameterTemplateMarkup[] Parameters { get; set; }
    }
}
