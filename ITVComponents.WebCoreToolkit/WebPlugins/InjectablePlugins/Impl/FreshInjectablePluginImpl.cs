using System;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

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

        public FreshInjectablePluginImpl(IServiceProvider services)
        {
            this.services = services;
        }

        public IPluginLease<T> Lease(string name = null)
        {
            IWebPluginHelper helper = services.GetRequiredService<IWebPluginHelper>();
            IPluginFactory scope = helper.CreateOperationScope(); // frischer, aufrufer-besessener Lade-Scope
            try
            {
                T plugin = name != null
                    ? scope[name, true] as T
                    // Standard-Namensaufloesung (inkl. Tenant-Prefix) wie der regulaere Injector, aber aus
                    // dem frischen Scope statt dem CurrentScope der Factory.
                    : new DefaultPluginInjector<T>().GetPluginInstance(services, scope, false);
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
