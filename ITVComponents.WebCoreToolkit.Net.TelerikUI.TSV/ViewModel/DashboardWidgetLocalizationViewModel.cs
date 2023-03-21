using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class DashboardWidgetLocalizationViewModel
    {
        public int DashboardWidgetLocalizationId { get; set; }
        public int DashboardWidgetId { get; set; }

        [Required, MaxLength(20)]
        public string LocaleName { get; set; }

        [Required, MaxLength(1024)]
        public string DisplayName { get; set; }

        [Required, MaxLength(2048)]
        public string TitleTemplate { get; set; }
        
        [Required, DataType(DataType.MultilineText)]
        public string Template { get; set; }
    }
}
