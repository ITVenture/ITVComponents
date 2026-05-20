using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TreeCustomerOnboarding.Models
{
    public class HierarchyEmployee : EmployeeBase<HierarchyTenant, string, User, HierarchyTenantUser, Role, HierarchyCompanyInfo, HierarchyEmployee, HierarchyEmployeeRole>
    {
    }
}
