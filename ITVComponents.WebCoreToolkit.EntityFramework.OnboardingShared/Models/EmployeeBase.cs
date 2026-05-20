using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models
{
    /// <summary>
    /// Generic base for an Employee within a customer-onboarded Tenant.
    /// Concrete derivatives bind the type parameters to the consumer-specific Tenant / User / TenantUser / Role types.
    /// </summary>
    public abstract class EmployeeBase<TTenant, TUserId, TUser, TTenantUser, TRole, TCompanyInfo, TEmployee, TEmployeeRole>
        where TTenant : Tenant
        where TUser : class
        where TTenantUser : class
        where TRole : class
        where TCompanyInfo : class
        where TEmployee : class
        where TEmployeeRole : class
    {
        public int EmployeeId { get; set; }

        public int CompanyInfoId { get; set; }

        public int TenantId { get; set; }

        public TUserId UserId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string EMail { get; set; }

        public InvitationStatus InvitationStatus { get; set; }

        public int? TenantUserId { get; set; }

        [ForeignKey(nameof(CompanyInfoId))]
        public virtual TCompanyInfo Company { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual TUser User { get; set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TTenantUser TenantUser { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        public virtual ICollection<TEmployeeRole> Roles { get; set; } = new List<TEmployeeRole>();
    }
}
