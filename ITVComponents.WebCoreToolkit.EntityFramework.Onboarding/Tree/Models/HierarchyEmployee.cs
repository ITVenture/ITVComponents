using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models
{
    public class HierarchyEmployee : EmployeeBase<HierarchyTenant, string, User, HierarchyTenantUser, Role, HierarchyBillingProfile, HierarchyEmployee, HierarchyEmployeeRole>
    {
    }
}
