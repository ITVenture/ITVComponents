using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels
{
    public class HierarchyWebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter> : WebPluginGenericParameter<TTenant,TWebPlugin,TWebPluginGenericParameter>
        where TTenant : HierarchyTenant
        where TWebPlugin : HierarchyWebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginGenericParameter : HierarchyWebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    {
    }

    public class HierarchyWebPluginGenericParameter : HierarchyWebPluginGenericParameter<HierarchyTenant, HierarchyWebPlugin, HierarchyWebPluginGenericParameter>
    {
    }
}
