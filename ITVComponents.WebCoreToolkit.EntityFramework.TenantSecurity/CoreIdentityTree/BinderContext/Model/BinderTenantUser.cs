using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.BinderModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.BinderContext.Model
{
    [BinderEntity]
    public class BinderTenantUser:BinderTenantUser<string, BinderUser>
    {
    }
}
