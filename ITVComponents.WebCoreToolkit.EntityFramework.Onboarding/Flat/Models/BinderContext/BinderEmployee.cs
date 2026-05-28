using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext.Model;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models.BinderContext
{
    [BinderEntity, ForeignKeySelection(typeof(EmployeeSelector))]
    public class BinderEmployee
    {
        [Key]
        public int EmployeeId { get; set; }

        public int BillingProfileId { get; set; }

        public int TenantId { get; set; }

        public string UserId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string EMail { get; set; }

        public int? TenantUserId { get; set; }

        [ForeignKey(nameof(BillingProfileId))]
        public virtual BinderBillingProfile BillingProfile { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual BinderUser User { get; set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual BinderTenantUser TenantUser { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual BinderTenant Tenant { get; set; }
    }
}
