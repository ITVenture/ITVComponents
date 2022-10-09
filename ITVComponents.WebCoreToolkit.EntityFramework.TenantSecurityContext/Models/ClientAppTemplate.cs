using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class ClientAppTemplate: ClientAppTemplate<int, User, Role, Permission, UserRole, RolePermission, TenantUser,AppPermission, AppPermissionSet, ClientAppTemplate, ClientAppTemplatePermission>
    {
    }
}
