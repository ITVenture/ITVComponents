using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.EFRepo.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.BinderModels
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
        public virtual TreeShared.Models.BinderModels.BinderTenant Tenant { get; set; }
    }
}
