﻿using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class Permission: WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base.Permission<Tenant,string, User, Role,Permission,UserRole,RolePermission,TenantUser, RoleRole>
    {
    }
}
