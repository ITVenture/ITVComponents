using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models
{
    public class HierarchyBillingProfile : BillingProfileBase<HierarchyTenant, string, User, HierarchyTenantUser, HierarchyEmployee, HierarchyDefaultAddress, HierarchyInvoiceAddress>
    {
    }
}
