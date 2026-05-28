using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models
{
    public class HierarchyBillingProfile : BillingProfileBase<HierarchyTenant, string, User, HierarchyTenantUser, HierarchyEmployee, HierarchyDefaultAddress, HierarchyInvoiceAddress>
    {
    }
}
