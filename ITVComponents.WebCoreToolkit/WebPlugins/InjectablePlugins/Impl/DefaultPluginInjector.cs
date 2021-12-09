using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl
{
    public class DefaultPluginInjector<T>:CustomPluginInjector<T> where T:class,IPlugin
    {
        /// <summary>
        /// Estimates the raw-Plugin-Name  (without extending it with the current permission scope)
        /// </summary>
        /// <param name="services">the services of the current http-request</param>
        /// <returns>the raw-name of the requested plugin</returns>
        protected virtual string GetScopelessUniqueName(IServiceProvider services)
        {
            var t = typeof(T);
            string name = t.Name;
            if (Attribute.GetCustomAttribute(t, typeof(ScopedDependencyAttribute)) is ScopedDependencyAttribute scopeAttr)
            {
                name = scopeAttr.FriendlyName ?? name;
            }

            return name;
        }

        protected virtual bool CheckPluginName(IServiceProvider services, bool prefixWithArea, ref string rawName)
        {
            var selector = services.GetService<IWebPluginsSelector>();
            var scopeProvider = services.GetService<IPermissionScope>();
            if (selector != null)
            {
                var userProvider = services.GetService<IContextUserProvider>();
                if (userProvider!= null)
                {
                    Dictionary<string, object> formatHints = new Dictionary<string, object>(userProvider.RouteData);
                    formatHints.Add("rawName",rawName);
                    var prefixed = prefixWithArea ? "[area][rawName]" : "[rawName]";
                    prefixed = formatHints.FormatText(prefixed);
                    if (selector.GetPlugin(prefixed) != null)
                    {
                        rawName = prefixed;
                        return true;
                    }

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the name of the Plugin to create with this injector
        /// </summary>
        /// <param name="services">the service-collection that contains services required to estimate the name</param>
        /// <param name="prefixWithArea">indicates whether to check for a prefix with the given area-prefix</param>
        /// <returns>the estimated proxy-name</returns>
        protected override string GetPluginUniqueName(IServiceProvider services, bool prefixWithArea)
        {
            
            var name = GetScopelessUniqueName(services);
            var ok = CheckPluginName(services, prefixWithArea, ref name);
            if (!ok && prefixWithArea)
            {
                ok = CheckPluginName(services, false, ref name);
            }

            if (ok)
            {
                return name;
            }

            return null;
        }
    }
}
