using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.EFRepo.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.BinderModels
{
    [BinderEntity]
    public class BinderTenant
    {

        [Key]
        public int TenantId { get; set; }

        [Required, MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        public int? ParentTenantId { get; set; }

        [ForeignKey(nameof(ParentTenantId))]
        public virtual BinderTenant? ParentTenant { get; set; }
    }
}
