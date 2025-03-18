using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class AssetTemplateFeature : AssetTemplateFeature<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature>
    {
    }
}
