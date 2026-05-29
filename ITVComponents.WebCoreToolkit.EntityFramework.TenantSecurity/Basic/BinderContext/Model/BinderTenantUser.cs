using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.BinderContext.Model
{
    [BinderEntity]
    public class BinderTenantUser:BinderTenantUser<int, BinderUser>
    {
    }
}
