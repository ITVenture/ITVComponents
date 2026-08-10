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

        /// <summary>
        /// True when this widget belongs to the default collection that a user without own widgets sees.
        /// Only meaningful on a template (<see cref="UserWidgetId"/> == 0).
        /// </summary>
        public bool InitiallyActive { get; set; }

        /// <summary>
        /// Width of the tile in grid columns. 0 means "not set" and reads as 1.
        /// </summary>
        public int ColSpan { get; set; }

        /// <summary>
        /// The user's parameter input as a JSON object (field name -&gt; invariant value), null when the
        /// widget has no parameters. Kept alongside the already-substituted
        /// <see cref="CustomQueryString"/> because the individual values cannot be recovered from it.
        /// </summary>
        public string ParamValues { get; set; }

        public int UserWidgetId { get; set; }

        public ICollection<DashboardParamDefinition> Params { get; set; } = new List<DashboardParamDefinition>();
    }
}
