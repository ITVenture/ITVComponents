using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class CustomUserProperty:WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base.CustomUserProperty<int, User>
    {
    }
}
