using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class UserWidget
    {
        [Key]
        public int UserWidgetId { get; set; }

        public int TenantId { get; set; }

        public string UserName { get; set; }

        public int DashboardWidgetId { get; set; }

        public int SortOrder { get; set; }

        [ForeignKey(nameof(DashboardWidgetId))]
        public virtual DashboardWidget Widget { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
