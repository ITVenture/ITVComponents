using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Helpers
{
    public class HealthPlugins
    {
        private readonly IBaseTenantContext context;
        private PluginFactory factory = new() { AllowFactoryParameter = true };

        public HealthPlugins(IBaseTenantContext context, IContextUserProvider up)
        {
            this.context = context;
            factory.UnknownConstructorParameter += ResolveReference;
        }

        private void ResolveReference(object sender, UnknownConstructorParameterEventArgs e)
        {
            
        }
    }
}
