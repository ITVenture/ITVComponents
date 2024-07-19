using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.BinderContext.Model
{
    [BinderEntity]
    public class BinderTenantUser:BinderTenantUser<int, BinderUser>
    {
    }
}
