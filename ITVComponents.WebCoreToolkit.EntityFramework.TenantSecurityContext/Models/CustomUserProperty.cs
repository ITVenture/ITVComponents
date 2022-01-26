using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class CustomUserProperty:WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.CustomUserProperty<int, User>
    {
    }
}
