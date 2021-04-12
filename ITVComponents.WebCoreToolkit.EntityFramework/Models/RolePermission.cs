using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    [Index(nameof(RoleId), nameof(PermissionId), nameof(TenantId),IsUnique=true,Name="IX_UniqueRolePermission")]
    public class RolePermission
    {
        [Key]
        public int RolePermissionId { get; set; }

        public int RoleId { get;set; }

        public int PermissionId { get; set; }

        public int TenantId { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual Permission Permission{ get;set; }

        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
