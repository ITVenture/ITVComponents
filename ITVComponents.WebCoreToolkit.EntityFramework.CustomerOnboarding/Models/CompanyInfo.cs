using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models
{
    public class CompanyInfo
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

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }

        public virtual DefaultAddress DefaultAddress { get; set; }

        public virtual InvoiceAddress InvoiceAddress { get; set; }

        [ForeignKey(nameof(CompanyAdminTenantUserId))]
        public virtual TenantUser Admin { get; set; }

        [ForeignKey(nameof(OwnerUserId))]
        public virtual User Owner { get; set; }

        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
