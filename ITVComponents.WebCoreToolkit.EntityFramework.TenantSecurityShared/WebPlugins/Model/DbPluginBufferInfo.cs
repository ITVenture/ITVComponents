using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Model
{
    internal class DbPluginBufferInfo<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TTenant : Tenant 
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPlugin: WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    {
        public TWebPlugin Plugin { get; set; }
        public DateTime Created { get; set; }
    }
}
