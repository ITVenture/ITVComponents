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

        public string LocalRef { get; set; }

        public DBWidgetParam[] Params { get; set; }
        public string TitleTemplate { get; set; }
    }
}
