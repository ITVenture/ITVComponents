using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl;

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
        public static InjectablePluginOptions ConfigureInjectablePlugin<T>(this InjectablePluginOptions options, Func<CustomPluginInjector<T>> configure) where T : class, IPlugin
        {
            var item = configure();
            options.AddInjector(item);
            return options;
        }

        /// <summary>
        /// Bindet <see cref="IInjectablePlugin{T}"/> fuer <typeparamref name="T"/> an die im DI-Container
        /// registrierte Instanz (statt an die Plugin-Factory). Kurzform fuer den Ein-Kontext-Fall: der
        /// Konsument haengt an <c>IInjectablePlugin&lt;T&gt;</c>, bekommt aber den regulaeren DI-Service.
        /// Im Per-Tenant-Fall diese Zeile weglassen - dann laedt der Standard-Injector das Plugin
        /// tenant-spezifisch.
        /// </summary>
        /// <typeparam name="T">der (Plugin-)Typ, der aus der DI bezogen wird</typeparam>
        /// <param name="options">die Plugin-Injection-Optionen</param>
        /// <param name="disposeWithContext">
        /// Erlaubt zusaetzlich die Nutzung ueber den Fresh-Weg (<see cref="IFreshInjectablePlugin{T}"/>). Nur
        /// setzen, wenn <typeparamref name="T"/> in der DI so registriert ist, dass je Lease eine frische,
        /// aufrufer-besessene und mit dem Scope disposbare Instanz entsteht. Default <c>false</c>: der
        /// Fresh-Weg wirft dann bewusst, um „frische" und „geteilte" Services klar zu trennen.
        /// </param>
        /// <returns>die uebergebenen Optionen (Method-Chaining)</returns>
        public static InjectablePluginOptions UseServiceInstance<T>(this InjectablePluginOptions options, bool disposeWithContext = false)
            where T : class, IPlugin
            => options.ConfigureInjectablePlugin<T>(() => new ServiceProviderPluginInjector<T>(disposeWithContext));
    }
}
