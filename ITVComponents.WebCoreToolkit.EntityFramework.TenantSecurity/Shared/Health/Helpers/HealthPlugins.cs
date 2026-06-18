using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Health.Helpers
{
    public class HealthPlugins
    {
        private readonly IToolkitContextFactory contextFactory;
        private PluginFactory factory = new() { AllowFactoryParameter = true };

        public HealthPlugins(IToolkitContextFactory contextFactory, IContextUserProvider up)
        {
            this.contextFactory = contextFactory;
            factory.UnknownConstructorParameter += ResolveReference;
        }

        private void ResolveReference(object sender, UnknownConstructorParameterEventArgs e)
        {
            
        }
    }
}
