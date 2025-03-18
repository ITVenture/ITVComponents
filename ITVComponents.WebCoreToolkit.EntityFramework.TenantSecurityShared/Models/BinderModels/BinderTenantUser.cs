using ITVComponents.EFRepo.DataAnnotations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels
{
    [BinderEntity]
    public class BinderTenantUser<TUserId, TUser>
    {
        [Key]
        public int TenantUserId { get; set; }

        public TUserId UserId { get; set; }

        public int TenantId { get; set; }

        public bool? Enabled { get; set; } = true;

        [ForeignKey(nameof(UserId))]
        public virtual TUser User { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual BinderTenant Tenant { get; set; }
    }
}
