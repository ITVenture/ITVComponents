using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels
{
    public class HierarchyWebPluginConstant<TTenant> : WebPluginConstant<TTenant>
    where TTenant: HierarchyTenant
    {
        public bool Inheritable { get; set; } = true;
    }

    public class HierarchyWebPluginConstant:HierarchyWebPluginConstant<HierarchyTenant>
    {
    }
}
