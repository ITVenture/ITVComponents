using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models
{
    public class CustomUserProperty:WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.CustomUserProperty<string, User>
    {
    }
}
