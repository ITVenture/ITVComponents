using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models
{
    public class CompanyInfo : CompanyInfoBase<Tenant, string, User, TenantUser, Employee, DefaultAddress, InvoiceAddress>
    {
    }
}
