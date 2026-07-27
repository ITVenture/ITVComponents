using System;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl
{
    /// <summary>
    /// Ein <see cref="CustomPluginInjector{T}"/>, der den Plugin-Typ NICHT aus der Plugin-Factory laedt,
    /// sondern schlicht die im DI-Container registrierte Instanz zurueckgibt. Damit laesst sich ein
    /// <see cref="IInjectablePlugin{T}"/>-Konsument in EINEM Umfeld ueber ein tenant-spezifisches Plugin
    /// bedienen (kein Injector registriert -&gt; Standard-Plugin-Aufloesung) und in einem anderen ueber die
    /// regulaere DI (diesen Injector fuer T registrieren). Der Konsument haengt in beiden Faellen nur an
    /// <see cref="IInjectablePlugin{T}"/> - ein Konstruktor, keine DI-Mehrdeutigkeit.
    /// </summary>
    /// <remarks>
    /// Registrierung (im Ein-Kontext-Fall):
    /// <code>
    /// services.Configure&lt;InjectablePluginOptions&gt;(o =&gt; o.UseServiceInstance&lt;MyContext&gt;());
    /// </code>
    /// Im Per-Tenant-Fall wird KEIN Injector fuer T registriert; dann greift der
    /// <c>DefaultPluginInjector</c> und laedt das Plugin tenant-spezifisch aus der Factory.
    /// </remarks>
    /// <typeparam name="T">der (Plugin-)Typ, der aus der DI bezogen wird</typeparam>
    public sealed class ServiceProviderPluginInjector<T> : CustomPluginInjector<T> where T : class, IPlugin
    {
        /// <summary>
        /// Wird bei diesem Injector NICHT verwendet (er umgeht die Plugin-Factory), muss aber - weil
        /// <see cref="CustomPluginInjector{T}.GetPluginUniqueName"/> abstrakt ist - implementiert werden.
        /// </summary>
        protected override string GetPluginUniqueName(IServiceProvider services, bool prefixWithArea)
            => typeof(T).Name;

        /// <inheritdoc/>
        public override T GetPluginInstance(IServiceProvider services, bool prefixWithArea)
            => services.GetService<T>();

        /// <inheritdoc/>
        public override T GetPluginInstance(IServiceProvider services, string explicitRequestedName)
            => services.GetService<T>();
    }
}
