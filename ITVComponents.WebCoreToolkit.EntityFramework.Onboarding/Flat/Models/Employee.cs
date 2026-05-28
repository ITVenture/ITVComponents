using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models
{
    public class Employee : EmployeeBase<Tenant, string, User, TenantUser, Role, BillingProfile, Employee, EmployeeRole>
    {
    }
}
