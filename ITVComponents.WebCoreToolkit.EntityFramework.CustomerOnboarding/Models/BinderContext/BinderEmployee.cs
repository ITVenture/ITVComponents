using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext.Model;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models.BinderContext
{
    [BinderEntity, ForeignKeySelection(typeof(EmployeeSelector))]
    public class BinderEmployee
    {
        [Key]
        public int EmployeeId { get; set; }

        public int CompanyInfoId { get; set; }

        public int TenantId { get; set; }

        public string UserId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string EMail { get; set; }

        public int? TenantUserId { get; set; }

        [ForeignKey(nameof(CompanyInfoId))] 
        public virtual BinderCompany Company { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual BinderUser User { get; set; }

        [ForeignKey(nameof(TenantUserId))] 
        public virtual BinderTenantUser TenantUser { get; set; }

        [ForeignKey(nameof(TenantId))] 
        public virtual BinderTenant Tenant { get; set; }
    }
}
