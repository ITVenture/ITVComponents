using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Generic base for an Employee within a customer-onboarded Tenant.
    /// Concrete derivatives bind the type parameters to the consumer-specific Tenant / User / TenantUser / Role types.
    /// For personal-profile tenants there are typically zero employee rows; company-profile
    /// tenants hold one or more employee rows (the owner plus any invited users).
    /// </summary>
    public abstract class EmployeeBase<TTenant, TUserId, TUser, TTenantUser, TRole, TBillingProfile, TEmployee, TEmployeeRole>
        where TTenant : Tenant
        where TUser : class
        where TTenantUser : class
        where TRole : class
        where TBillingProfile : class
        where TEmployee : class
        where TEmployeeRole : class
    {
        [Key]
        public int EmployeeId { get; set; }

        public int BillingProfileId { get; set; }

        public int TenantId { get; set; }

        public TUserId UserId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string EMail { get; set; }

        public InvitationStatus InvitationStatus { get; set; }

        public int? TenantUserId { get; set; }

        [ForeignKey(nameof(BillingProfileId))]
        public virtual TBillingProfile BillingProfile { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual TUser User { get; set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TTenantUser TenantUser { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        public virtual ICollection<TEmployeeRole> Roles { get; set; } = new List<TEmployeeRole>();
    }
}
