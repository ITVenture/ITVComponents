using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model
{
    public class AssetTemplateFeature : AssetTemplateFeature<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature>
    {
    }
}
