using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    internal class InjectablePluginImpl<T>:IInjectablePlugin<T> where T:class, IPlugin
    {
        private readonly IServiceProvider services;
        private readonly IOptions<InjectablePluginOptions> options;
        private T instance;
        public InjectablePluginImpl(IServiceProvider services, IOptions<InjectablePluginOptions> options)
        {
            this.services = services;
            this.options = options;
        }

        public T Instance => instance ??= GetInstance();
        public T GetInstance(string name)
        {
            var opt = options.Value;
            var retVal = opt.GetPlugIn<T>(services, name);
            if (retVal == null)
            {
                var defaultInjector = new DefaultPluginInjector<T>();
                retVal = defaultInjector.GetPluginInstance(services, name);
            }

            return retVal;
        }


        private T GetInstance()
        {
            var start = DateTime.Now;
            string name = "";
            try
            {
                var opt = options.Value;
                var retVal = opt.GetPlugIn<T>(services);
                if (retVal == null)
                {
                    var defaultInjector = new DefaultPluginInjector<T>();
                    retVal = defaultInjector.GetPluginInstance(services, opt.CheckForAreaPrefixedNames);
                    name = retVal.UniqueName;
                }

                return retVal;
            }
            finally
            {
                LogEnvironment.LogDebugEvent($"Total-Duration for {name}: {DateTime.Now.Subtract(start).TotalSeconds}", LogSeverity.Report);
            }
        }

        public void Dispose()
        {
            instance = null;
        }
    }
}
