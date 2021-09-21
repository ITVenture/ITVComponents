using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class DashboardWidgetDefinition
    {
        public string DisplayName { get; set; }

        public string SystemName { get; set; }

        public DiagnosticsQueryDefinition DiagnosticsQuery { get; set; }

        public string Area { get; set; }

        public string CustomQueryString { get; set; }

        public string Template { get; set; }
    }
}
