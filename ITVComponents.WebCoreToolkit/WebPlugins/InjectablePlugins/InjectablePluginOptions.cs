using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    public class InjectablePluginOptions
    {
        /// <summary>
        /// holds a list of usable proxy-injectors
        /// </summary>
        private Dictionary<Type, ICustomPluginInjector> injectors = new Dictionary<Type, ICustomPluginInjector>();

        /// <summary>
        /// Indicates whether to check for pluginNames with additional Area-Prefix and without Are-Prefix as fallback
        /// </summary>
        public bool CheckForAreaPrefixedNames { get; set; } = false;

        /// <summary>
        /// Registers an injector to the list of available proxy-injectors
        /// </summary>
        /// <typeparam name="T">the interface of which a proxy needs to be created</typeparam>
        /// <param name="injector">the injector used to create the demanded instance</param>
        public void AddInjector<T>(CustomPluginInjector<T> injector) where T : class,IPlugin
        {
            var t = typeof(T);
            injectors.Add(t, injector);
        }

        /// <summary>
        /// Creates the requested proxy object
        /// </summary>
        /// <typeparam name="T">the proxy-type to return</typeparam>
        /// <param name="services">a services collection that provides required services</param>
        /// <returns>the created proxy instance</returns>
        internal T GetPlugIn<T>(IServiceProvider services) where T:class,IPlugin
        {
            if (!injectors.ContainsKey(typeof(T)))
            {
                return null;
            }

            var injector = (CustomPluginInjector<T>) injectors[typeof(T)];
            return injector.GetPluginInstance(services, CheckForAreaPrefixedNames);
        }
        
        /// <summary>
        /// Creates the requested proxy object
        /// </summary>
        /// <typeparam name="T">the proxy-type to return</typeparam>
        /// <param name="services">a services collection that provides required services</param>
        /// <param name="scope">the scope that contains the plugin</param>
        /// <returns>the created proxy instance</returns>
        internal T GetPlugIn<T>(IServiceProvider services, IPluginFactory scope, string explicitRequestedName) where T : class, IPlugin
        {
            if (!injectors.ContainsKey(typeof(T)))
            {
                return null;
            }

            var injector = (CustomPluginInjector<T>)injectors[typeof(T)];
            if (!injector.DisposeWithContext)
            {
                // Der Fresh-Weg (IFreshInjectablePlugin) besitzt die Instanz ueber den frischen Lade-Scope und
                // disposed sie mit ihm. Ein Injector, der die Instanz aus einem aeusseren ServiceProvider bezieht
                // (nicht scope-besessen), gehoert hier NICHT hin - sonst gaebe es unter Nebenlaeufigkeit eine
                // geteilte Instanz bzw. ein Disposal-Leck. Klar trennen statt still das Falsche liefern.
                throw new InvalidOperationException(
                    $"The injector registered for '{typeof(T).Name}' is not configured for fresh leases " +
                    $"(IFreshInjectablePlugin): it does not declare DisposeWithContext. Either consume it via " +
                    $"IInjectablePlugin<{typeof(T).Name}> (shared instance), or - if the DI registration really " +
                    $"produces a fresh, scope-owned instance per lease - opt in explicitly " +
                    $"(e.g. UseServiceInstance<{typeof(T).Name}>(disposeWithContext: true)).");
            }

            if (string.IsNullOrEmpty(explicitRequestedName))
            {
                return injector.GetPluginInstance(services, scope, CheckForAreaPrefixedNames);
            }

            return injector.GetPluginInstance(services, scope, explicitRequestedName);
        }

        /// <summary>
        /// Creates the requested proxy object
        /// </summary>
        /// <typeparam name="T">the proxy-type to return</typeparam>
        /// <param name="services">a services collection that provides required services</param>
        /// <returns>the created proxy instance</returns>
        internal T GetPlugIn<T>(IServiceProvider services, string explicitRequestedName) where T : class, IPlugin
        {
            if (!injectors.ContainsKey(typeof(T)))
            {
                return null;
            }

            var injector = (CustomPluginInjector<T>)injectors[typeof(T)];
            return injector.GetPluginInstance(services, explicitRequestedName);
        }
    }
}
