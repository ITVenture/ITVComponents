using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models
{
    public class Employee : EmployeeBase<Tenant, string, User, TenantUser, Role, BillingProfile, Employee, EmployeeRole>
    {
    }
}
