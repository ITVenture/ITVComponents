using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class ClientAppTemplate: ClientAppTemplate<string, User, Role, Permission, UserRole, RolePermission, TenantUser,AppPermission, AppPermissionSet, ClientAppTemplate, ClientAppTemplatePermission>
    {
    }
}
