using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class CustomUserProperty:WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.CustomUserProperty<string, User>
    {
    }
}
