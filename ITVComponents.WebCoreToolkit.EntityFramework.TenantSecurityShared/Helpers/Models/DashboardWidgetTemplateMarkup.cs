using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class DashboardWidgetTemplateMarkup
    {
        public string DisplayName { get; set; }

        public string TitleTemplate { get; set; }

        public string SystemName { get; set; }

        public string DiagnosticsQueryName { get; set; }

        public string Area { get; set; }

        public string CustomQueryString { get; set; }

        public string Template { get; set; }

        public DashboardParamTemplateMarkup[] Parameters { get; set; }
    }
}
