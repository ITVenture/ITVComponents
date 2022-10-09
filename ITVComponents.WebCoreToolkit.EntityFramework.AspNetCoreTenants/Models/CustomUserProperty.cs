using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class CustomUserProperty:WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.CustomUserProperty<string, User>
    {
    }
}
