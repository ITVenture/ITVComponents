using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models.BinderContext
{
    [BinderEntity]
    public class BinderCompany
    {
        [Key]
        public int CompanyInfoId { get; set; }

        public string OwnerUserId { get; set; }

        public int? CompanyAdminTenantUserId { get; set; }

        public int? TenantId { get; set; }
        //inv
        public bool UseInvoiceAddr { get; set; }

        [MaxLength(100)]
        public string PhoneNumber { get; set; }

        [MaxLength(256)]
        public string Email { get; set; }

        [ForeignKey(nameof(CompanyAdminTenantUserId))]
        public virtual BinderTenantUser Admin { get; set; }

        [ForeignKey(nameof(OwnerUserId))]
        public virtual BinderUser Owner { get; set; }

        public virtual ICollection<BinderEmployee> Employees { get; set; } = new List<BinderEmployee>();

        [ForeignKey(nameof(TenantId))]
        public virtual BinderTenant Tenant { get; set; }
    }
}
