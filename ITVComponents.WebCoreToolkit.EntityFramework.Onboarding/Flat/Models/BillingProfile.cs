using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models
{
    public class BillingProfile : BillingProfileBase<Tenant, string, User, TenantUser, Employee, DefaultAddress, InvoiceAddress>
    {
    }
}
