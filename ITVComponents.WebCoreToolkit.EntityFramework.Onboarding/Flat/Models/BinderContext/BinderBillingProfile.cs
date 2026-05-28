using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models.BinderContext
{
    [BinderEntity]
    public class BinderBillingProfile
    {
        [Key]
        public int BillingProfileId { get; set; }

        public ProfileType ProfileType { get; set; }

        public string OwnerUserId { get; set; }

        public int? CompanyAdminTenantUserId { get; set; }

        public int? TenantId { get; set; }

        public bool UseInvoiceAddr { get; set; }

        [MaxLength(100)]
        public string PhoneNumber { get; set; }

        [MaxLength(256)]
        public string Email { get; set; }

        [MaxLength(256)]
        public string FirstName { get; set; }

        [MaxLength(256)]
        public string LastName { get; set; }

        [MaxLength(1024)]
        public string CompanyName { get; set; }

        [MaxLength(64)]
        public string VatNumber { get; set; }

        [ForeignKey(nameof(CompanyAdminTenantUserId))]
        public virtual BinderTenantUser Admin { get; set; }

        [ForeignKey(nameof(OwnerUserId))]
        public virtual BinderUser Owner { get; set; }

        public virtual ICollection<BinderEmployee> Employees { get; set; } = new List<BinderEmployee>();

        [ForeignKey(nameof(TenantId))]
        public virtual BinderTenant Tenant { get; set; }
    }
}
