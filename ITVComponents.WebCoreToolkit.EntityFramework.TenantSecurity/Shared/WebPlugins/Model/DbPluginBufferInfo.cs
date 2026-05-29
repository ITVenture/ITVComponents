using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins.Model
{
    public class DbPluginBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TTenant : Tenant 
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPlugin: WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>*/
    {
        public int? WebPluginId { get; set; }
        public WebPlugin Plugin { get; set; }
        public DateTime Created { get; set; }
    }
}
