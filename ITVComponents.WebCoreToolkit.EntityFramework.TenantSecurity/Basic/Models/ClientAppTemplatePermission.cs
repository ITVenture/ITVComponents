using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models
{
    public class ClientAppTemplatePermission: ClientAppTemplatePermission<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AppPermission, AppPermissionSet, ClientAppTemplate, ClientAppTemplatePermission>
    {
    }
}
