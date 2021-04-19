using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class TenantUser
    {
        [Key]
        public int TenantUserId { get; set; }

        public int UserId { get; set; }

        public int TenantId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User{get;set;}

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant{get; set; }

        public virtual ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    }
}
