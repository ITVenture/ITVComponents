using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels
{
    public class HierarchyWebPlugin<TTenant,TWebPlugin, TWebPluginGenericParameter> : WebPlugin<TTenant,TWebPlugin,TWebPluginGenericParameter>
    where TTenant:HierarchyTenant
    where TWebPlugin:HierarchyWebPlugin<TTenant,TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginGenericParameter: HierarchyWebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    {
        public bool Inheritable { get; set; } = true;
    }

    public class HierarchyWebPlugin:HierarchyWebPlugin<HierarchyTenant,HierarchyWebPlugin, HierarchyWebPluginGenericParameter>
    {
        //public bool Inheritable { get; set; } = true;
    }
}
