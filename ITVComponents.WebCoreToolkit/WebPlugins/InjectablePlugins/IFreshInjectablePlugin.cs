using System;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    /// <summary>
    /// Wie <see cref="IInjectablePlugin{T}"/>, aber <b>Blazor-tauglich</b>: jede <see cref="Lease"/> oeffnet
    /// einen EIGENEN frischen Lade-Scope (ueber <c>IWebPluginHelper.CreateOperationScope</c>) und liefert eine
    /// aufrufer-besessene, disposbare Instanz - kein geteiltes, circuit-scoped Objekt. Damit sind stateful
    /// Plugins (z.B. DbContext-artige) unter Blazor-Nebenlaeufigkeit sicher.
    /// </summary>
    /// <remarks>
    /// Nutzung: <c>using var lease = fresh.Lease(); var plugin = lease.Value; …</c> - das Dispose der Lease
    /// schliesst den Lade-Scope (und disposed die scope-owned Dependencies, z.B. den frischen DbContext). Der
    /// WebPluginHelper uebergibt die Scope-Verantwortung vollstaendig dem Aufrufer; er schliesst den
    /// Operations-Scope NICHT selbst. Fuer zustandslose Plugins genuegt weiterhin <see cref="IInjectablePlugin{T}"/>
    /// (auch unter Blazor).
    /// </remarks>
    /// <typeparam name="T">der (Plugin-)Typ, der frisch geladen wird</typeparam>
    public interface IFreshInjectablePlugin<T> where T : class, IPlugin
    {
        /// <summary>
        /// Oeffnet einen frischen Lade-Scope (aktueller Tenant-/Permission-Scope) und least das Plugin. Der
        /// Aufrufer disposed die Lease (schliesst den Scope).
        /// </summary>
        /// <param name="name">optionaler expliziter Plugin-Name; null = Standard-Namensaufloesung</param>
        IPluginLease<T> Lease(string name = null);
    }

    /// <summary>
    /// Ein disposbares Handle, das eine frisch geladene Plugin-Instanz UND ihren Lade-Scope besitzt. Das
    /// Dispose gibt beide frei.
    /// </summary>
    /// <typeparam name="T">der Typ der geleasten Instanz</typeparam>
    public interface IPluginLease<out T> : IDisposable where T : class, IPlugin
    {
        /// <summary>Die geleaste Instanz. Gueltig bis zum Dispose dieser Lease.</summary>
        T Value { get; }
    }
}
