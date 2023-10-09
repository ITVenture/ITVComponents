using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.WebPlugins;

namespace ITVComponents.GenericService.WebService.Services
{
    internal class ServicePluginHelper:IWebPluginHelper
    {
        private readonly IPluginFactory factory;

        public ServicePluginHelper(IPluginFactory factory)
        {
            this.factory = factory;
        }

        public IPluginFactory GetFactory()
        {
            return factory;
        }

        public IPluginFactory GetFactory(string explicitPluginScope)
        {
            LogEnvironment.LogEvent($"Explicit Tenant was requested. Returning default factory anyway.", LogSeverity.Warning);
            return factory;
        }

        public void ResetFactory()
        {
        }

        public void Dispose()
        {
        }
    }
}
