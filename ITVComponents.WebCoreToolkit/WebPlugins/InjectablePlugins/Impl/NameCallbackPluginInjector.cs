using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl
{
    public class NameCallbackPluginInjector<T>:DefaultPluginInjector<T> where T:class,IPlugin
    {
        /// <summary>
        /// The Callback that estimates the name of the plugin to inject
        /// </summary>
        private readonly Func<IServiceProvider, string, string> rawNameCallback;

        /// <summary>
        /// Indicates whether to prefix the estimated name with the current permission prefix
        /// </summary>
        private readonly bool prefixWithScope;

        /// <summary>
        /// Initializes a new instance of the NameCallbackPluginInjector class
        /// </summary>
        /// <param name="rawNameCallback">the rawNameCallback to calculate an appropriate name for the loaded plugin</param>
        /// <param name="prefixWithScope">indicates whether to automatically prefix the estimated plugin-name with the current permission-scope</param>
        public NameCallbackPluginInjector(Func<IServiceProvider, string, string> rawNameCallback, bool prefixWithScope)
        {
            this.rawNameCallback = rawNameCallback;
            this.prefixWithScope = prefixWithScope;
        }

        protected override string GetScopelessUniqueName(IServiceProvider services)
        {
            var ret = base.GetScopelessUniqueName(services);
            ret = rawNameCallback(services, ret);
            return ret;
        }

        /// <summary>
        /// Gets the name of the Plugin to create with this injector
        /// </summary>
        /// <param name="services">the service-collection that contains services required to estimate the name</param>
        /// <returns>the estimated proxy-name</returns>
        protected override string GetPluginUniqueName(IServiceProvider services, bool prefixWithArea)
        {
            if (prefixWithScope)
            {
                return base.GetPluginUniqueName(services, prefixWithArea);
            }

            var retVal = GetScopelessUniqueName(services);
            var ok = CheckPluginName(services, prefixWithArea, ref retVal);
            if (!ok && prefixWithArea)
            {
                ok = CheckPluginName(services,false, ref retVal);
            }

            if (ok)
            {
                return retVal;
            }

            return null;
        }
    }
}
