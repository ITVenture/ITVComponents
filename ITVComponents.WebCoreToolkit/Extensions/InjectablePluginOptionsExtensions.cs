using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class InjectablePluginOptionsExtensions
    {
        /// <summary>
        /// Configures a new proxy instance for the given Injection options
        /// </summary>
        /// <typeparam name="T">the type for which a Plugin must be created</typeparam>
        /// <param name="options">the options that are used for Plugin-injection</param>
        /// <param name="configure">a callback that allows the custom configuration of the Plugin-settings</param>
        /// <returns>the Plugin-options that were passed initially for method-chaining.</returns>
        public static InjectablePluginOptions ConfigureInjectablePlugin<T>(this InjectablePluginOptions options, Func<CustomPluginInjector<T>> configure) where T : class
        {
            var item = configure();
            options.AddInjector(item);
            return options;
        }
    }
}
