using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class DashboardWidget
    {
        [Key]
        public int DashboardWidgetId { get; set; }

        [MaxLength(100)]
        public string DisplayName { get; set; }

        [MaxLength(100)]
        public string SystemName { get; set; }

        public int DiagnosticsQueryId { get; set; }

        public string Area { get; set; }

        public string CustomQueryString { get; set; }

        public string Template { get; set; }


        [ForeignKey(nameof(DiagnosticsQueryId))]
        public virtual DiagnosticsQuery DiagnosticsQuery { get; set; }
    }
}
