using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    [Index(nameof(RoleId), nameof(TenantUserId),IsUnique=true,Name="IX_UniqueUserRole")]
    public class UserRole
    {
        [Key]
        public int UserRoleId { get; set; }

        public int TenantUserId { get; set; }

        public int RoleId { get;set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TenantUser User { get;set; }

        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; }
    }
}
