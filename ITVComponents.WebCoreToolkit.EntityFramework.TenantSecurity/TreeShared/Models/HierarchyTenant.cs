using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models
{
    public class HierarchyTenant:Tenant
    {
        public int? ParentTenantId { get; set; }

        [ForeignKey(nameof(ParentTenantId))]
        public virtual HierarchyTenant? ParentTenant { get; set; }

        [InverseProperty(nameof(ParentTenant))]
        public virtual ICollection<HierarchyTenant> Children { get; set; }
    }
}
