using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class DashboardParam
    {
        [Key]
        public int DashboardParamId { get; set; }

        public int DashboardWidgetId { get; set; }

        [MaxLength(128), Required]
        public string ParameterName { get; set; }

        public InputType InputType { get; set; }

        public string InputConfig { get; set; }

        [ForeignKey(nameof(DashboardWidgetId))]
        public virtual DashboardWidget Parent { get; set; }
    }
}
