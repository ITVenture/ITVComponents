using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class UserDashboardWidgetDefinition
    {
        public string UserName { get; set; }

        public string DashboardWidgetName { get; set; }

        public int SortOrder { get; set; }
        public DashboardWidgetDefinition Widget { get; set; }
    }
}
