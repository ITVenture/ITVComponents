using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class AssetTemplatePath : AssetTemplatePath<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature>
    {
    }
}
