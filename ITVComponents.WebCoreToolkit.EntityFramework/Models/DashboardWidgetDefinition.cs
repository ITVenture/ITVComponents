using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class DashboardWidgetDefinition
    {
        public int DashboardWidgetId { get; set; }
        public string DisplayName { get; set; }

        public string TitleTemplate { get; set; }

        public string SystemName { get; set; }

        public DiagnosticsQueryDefinition DiagnosticsQuery { get; set; }

        public string Area { get; set; }

        public string CustomQueryString { get; set; }

        public string Template { get; set; }

        public int SortOrder { get; set; }

        public int UserWidgetId { get; set; }

        public ICollection<DashboardParamDefinition> Params { get; set; } = new List<DashboardParamDefinition>();
    }
}
