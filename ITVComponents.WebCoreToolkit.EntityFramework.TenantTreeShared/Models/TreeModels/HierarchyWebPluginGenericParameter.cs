using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.FlatTenantModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels
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
