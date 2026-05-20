using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models
{
    /// <summary>
    /// Generic base for the per-tenant CompanyInfo entity. Concrete derivatives bind the
    /// type parameters to the consumer-specific Tenant / User / TenantUser / Employee / Address types.
    /// </summary>
    public abstract class CompanyInfoBase<TTenant, TUserId, TUser, TTenantUser, TEmployee, TDefaultAddress, TInvoiceAddress>
        where TTenant : Tenant
        where TUser : class
        where TTenantUser : class
        where TEmployee : class
        where TDefaultAddress : class
        where TInvoiceAddress : class
    {
        [Key]
        public int CompanyInfoId { get; set; }

        public TUserId OwnerUserId { get; set; }

        public int? CompanyAdminTenantUserId { get; set; }

        public int? TenantId { get; set; }

        public bool UseInvoiceAddr { get; set; }

        [MaxLength(100)]
        public string PhoneNumber { get; set; }

        [MaxLength(256)]
        public string Email { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        public virtual TDefaultAddress DefaultAddress { get; set; }

        public virtual TInvoiceAddress InvoiceAddress { get; set; }

        [ForeignKey(nameof(CompanyAdminTenantUserId))]
        public virtual TTenantUser Admin { get; set; }

        [ForeignKey(nameof(OwnerUserId))]
        public virtual TUser Owner { get; set; }

        public virtual ICollection<TEmployee> Employees { get; set; } = new List<TEmployee>();
    }
}
