using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.BinderContext.Model
{
    [BinderEntity]
    public class BinderTenantUser:BinderTenantUser<string, BinderUser>
    {
    }
}
