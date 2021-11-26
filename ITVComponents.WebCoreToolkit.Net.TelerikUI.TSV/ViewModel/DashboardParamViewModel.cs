using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class DashboardParamViewModel
    {
        public int DashboardParamId { get; set; }

        public int DashboardWidgetId { get; set; }

        [MaxLength(128), Required]
        public string ParameterName { get; set; }

        public InputType InputType { get; set; }

        [DataType(DataType.MultilineText)]
        public string InputConfig { get; set; }
    }
}
