using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }

        public int CompanyInfoId { get; set; }

        public int TenantId { get; set; }

        public string UserId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string EMail { get; set; }

        public InvitationStatus InvitationStatus { get; set; }

        public int? TenantUserId { get; set; }

        [ForeignKey(nameof(CompanyInfoId))]
        public virtual CompanyInfo Company { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TenantUser TenantUser { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }

        public virtual ICollection<EmployeeRole> Roles { get; set; } = new List<EmployeeRole>();
    }

    public enum InvitationStatus
    {
        None,
        Pending,
        Committed,
        Revoked
    }
}
