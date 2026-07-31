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
        /// Initialisiert eine neue Instanz der ServiceProviderPluginInjector-Klasse.
        /// </summary>
        /// <param name="disposeWithContext">
        /// Deklariert, dass die aus der DI bezogene Instanz mit dem frischen Lade-Scope
        /// (<see cref="IFreshInjectablePlugin{T}"/>) besessen und disposed wird - andernfalls wirft der
        /// Fresh-Weg. Nur setzen, wenn <typeparamref name="T"/> in der DI so registriert ist, dass je Lease
        /// eine frische, aufrufer-besessene Instanz entsteht (z.B. Transient bzw. per frischem Scope). Fuer den
        /// regulaeren <see cref="IInjectablePlugin{T}"/>-Weg (geteilte Instanz) ist das Flag ohne Bedeutung.
        /// </param>
        public ServiceProviderPluginInjector(bool disposeWithContext = false)
        {
            DisposeWithContext = disposeWithContext;
        }

        /// <inheritdoc/>
        public override bool DisposeWithContext { get; }

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
        public override T GetPluginInstance(IServiceProvider services, IPluginFactory scope, bool prefixWithArea)
            => services.GetService<T>();

        /// <inheritdoc/>
        public override T GetPluginInstance(IServiceProvider services, string explicitRequestedName)
            => services.GetService<T>();

        /// <inheritdoc/>
        public override T GetPluginInstance(IServiceProvider services, IPluginFactory scope, string explicitRequestedName)
            => services.GetService<T>();
    }
}
