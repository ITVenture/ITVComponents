using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels
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
