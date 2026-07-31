using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl
{
    /// <summary>
    /// Standard-Implementierung von <see cref="IFreshInjectablePlugin{T}"/>: oeffnet je Lease einen frischen
    /// Lade-Scope ueber <c>IWebPluginHelper.CreateOperationScope</c> (dort werden die scope-owned
    /// Dependencies - z.B. ein frischer DbContext - neu aufgeloest und mit dem Scope disposed), loest das
    /// Plugin darin auf und gibt eine <see cref="IPluginLease{T}"/> zurueck, deren Dispose den Scope schliesst.
    /// </summary>
    internal sealed class FreshInjectablePluginImpl<T> : IFreshInjectablePlugin<T> where T : class, IPlugin
    {
        private readonly IServiceProvider services;
        private readonly IOptions<InjectablePluginOptions> options;

        public FreshInjectablePluginImpl(IServiceProvider services, IOptions<InjectablePluginOptions> options)
        {
            this.services = services;
            this.options = options;
        }

        public IPluginLease<T> Lease(string name = null)
        {
            IWebPluginHelper helper = services.GetRequiredService<IWebPluginHelper>();
            IPluginFactory scope = helper.CreateOperationScope(); // frischer, aufrufer-besessener Lade-Scope
            var opt = options.Value;
            try
            {
                // Registrierter Injector zuerst (der Fresh-Guard in GetPlugIn stellt sicher, dass nur
                // scope-besessene Injectoren hierher gelangen); ohne registrierten Injector faellt es auf die
                // Standard-Namensaufloesung aus dem frischen Scope zurueck (inkl. Tenant-Prefix).
                var plugin = opt.GetPlugIn<T>(services, scope, name)
                             ??
                             new DefaultPluginInjector<T>().GetPluginInstance(services, scope, false);
                if (plugin == null)
                {
                    throw new InvalidOperationException(
                        $"No plugin could be resolved for a fresh '{typeof(T).Name}' lease " +
                        $"(name '{name ?? "<default>"}').");
                }

                return new PluginLease(plugin, scope);
            }
            catch
            {
                // Bei einem Fehlschlag den frisch geoeffneten Scope sofort wieder schliessen (kein Leck).
                scope.Dispose();
                throw;
            }
        }

        /// <summary>Besitzt die frisch geladene Instanz und ihren Lade-Scope; Dispose schliesst den Scope.</summary>
        private sealed class PluginLease : IPluginLease<T>
        {
            private readonly IPluginFactory scope;
            private bool disposed;

            public PluginLease(T value, IPluginFactory scope)
            {
                Value = value;
                this.scope = scope;
            }

            public T Value { get; }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                try
                {
                    // Schliesst den Lade-Scope und disposed die scope-owned Dependencies + das Plugin.
                    scope.Dispose();
                }
                catch (Exception ex)
                {
                    // Freigabe darf nicht mitreissen; der Fehler wird protokolliert (mit Stacktrace).
                    LogEnvironment.LogEvent(
                        $"Could not dispose the fresh plugin lease scope for '{typeof(T).Name}': " +
                        $"{ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }
    }
}
