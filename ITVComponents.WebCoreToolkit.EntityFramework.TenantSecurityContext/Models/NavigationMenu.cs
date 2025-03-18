using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class NavigationMenu: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.NavigationMenu<Tenant, int,User,Role,Permission,UserRole,RolePermission,TenantUser, RoleRole, NavigationMenu,TenantNavigationMenu>
    {
    }
}
