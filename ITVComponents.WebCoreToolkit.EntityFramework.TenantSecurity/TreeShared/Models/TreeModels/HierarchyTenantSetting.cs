using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels
{
    public class HierarchyTenantSetting<TTenant> : TenantSetting<TTenant>
    where TTenant: HierarchyTenant
    {
        public bool Inheritable { get; set; } = true;
    }

    public class HierarchyTenantSetting:HierarchyTenantSetting<HierarchyTenant>
    {
    }
}
