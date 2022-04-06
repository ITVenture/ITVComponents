using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class DashboardParamTemplateMarkup
    {
        public string ParameterName { get; set; }

        public InputType InputType { get; set; }

        public string InputConfig { get; set; }
    }
}
