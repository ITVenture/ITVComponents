using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.ViewModel
{
    public class DBWidget
    {
        public int DashboardWidgetId { get; set; }

        public int UserWidgetId { get; set; }
        public string DisplayName { get; set; }

        public string SystemName { get; set; }

        public string Area { get; set; }

        public string Template { get; set; }
        public string QueryName { get; set; }
        public string CustomQueryString { get; set; }

        public int SortOrder { get; set; }

        /// <summary>
        /// Width of the tile in grid columns (0 = not set, reads as 1). Carried through so a save from
        /// this endpoint does not reset a width set in the Blazor dashboard.
        /// </summary>
        public int ColSpan { get; set; }

        /// <summary>
        /// The user's parameter input as a JSON object. Carried through for the same reason as
        /// <see cref="ColSpan"/>: the store would otherwise take a missing value as "cleared".
        /// </summary>
        public string ParamValues { get; set; }

        public string LocalRef { get; set; }

        public DBWidgetParam[] Params { get; set; }
        public string TitleTemplate { get; set; }
    }
}
