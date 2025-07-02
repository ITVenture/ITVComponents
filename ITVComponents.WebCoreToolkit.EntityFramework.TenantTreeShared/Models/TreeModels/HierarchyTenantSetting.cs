using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels
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
