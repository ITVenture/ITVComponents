using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TreeCustomerOnboarding.Models
{
    public class HierarchyBillingProfile : BillingProfileBase<HierarchyTenant, string, User, HierarchyTenantUser, HierarchyEmployee, HierarchyDefaultAddress, HierarchyInvoiceAddress>
    {
    }
}
