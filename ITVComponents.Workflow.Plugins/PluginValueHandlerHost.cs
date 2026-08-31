using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.ValueHandles;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Loest Wert-Handler als Plugins aus der PluginFactory auf. Je Aufloesungsrunde wird ein Scope
    /// geoeffnet, die benannten Handler werden darin on demand geladen und beim Schliessen wieder
    /// freigegeben.
    /// </summary>
    /// <remarks>
    /// Dasselbe Muster wie <see cref="PluginActivityHost"/> - und aus demselben Grund: waehrend ein
    /// Vorgang wartet, haelt er keine Plugin-Ressourcen.
    /// <para>
    /// Aufgeloest wird ueber die <b>konfigurierten</b> Plugins des ausfuehrenden Mandanten, mit
    /// Typpruefung - dieselbe Latte wie beim <c>ActivityRef</c>, kein zusaetzliches Gatter. Bei einer
    /// <b>oeffentlichen</b> Definition (<c>TenantId = null</c>) faellt die Aufloesung im Scope des
    /// ausfuehrenden Mandanten: derselbe Name trifft dort, was dort unter ihm eingerichtet ist. Das ist
    /// gewollt - und es heisst, dass eine oeffentliche Definition mit einem Handler-Namen eine Aussage
    /// ueber die Einrichtung jedes Mandanten macht.
    /// </para>
    /// </remarks>
    public sealed class PluginValueHandlerHost : IValueHandlerHost
    {
        private readonly PluginFactory factory;

        /// <summary>
        /// Initialisiert den Host mit der Factory, aus deren Scope die Handler geladen werden.
        /// </summary>
        /// <param name="factory">die Factory, die die Handler-Plugins bereitstellt</param>
        public PluginValueHandlerHost(PluginFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc/>
        public IValueHandlerScope OpenScope(WorkflowInstance instance)
        {
            return new PluginValueHandlerScope(factory, instance);
        }

        /// <summary>
        /// Ein Aufloesungs-Kontext fuer eine Runde. Oeffnet den PluginFactory-Scope traege beim ersten
        /// aufgeloesten Handler und schliesst ihn (samt der geladenen Plugins) beim Dispose.
        /// </summary>
        private sealed class PluginValueHandlerScope : IValueHandlerScope
        {
            private readonly PluginFactory factory;
            private readonly WorkflowInstance instance;

            private IPluginFactory scope;

            public PluginValueHandlerScope(PluginFactory factory, WorkflowInstance instance)
            {
                this.factory = factory;
                this.instance = instance;
            }

            public IWorkflowValueHandler Resolve(string handlerName)
            {
                // Scope erst jetzt oeffnen: eine Runde ohne Handler zahlt nichts.
                // transientLoadingScope: false, damit die geladenen Plugins beim Schliessen disposed werden.
                scope ??= factory.NewScope(
                    new Dictionary<string, object> { { "instanceId", instance.Id } },
                    null,
                    false);

                if (scope[handlerName, true] is IValueHandlerPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den Handler-Namen '{handlerName}' konnte kein Workflow-Wert-Handler aufgeloest " +
                    "werden. Es muss ein Scoped-Plugin dieses Namens ueber einen IDynamicLoader der Factory " +
                    "bereitstehen (und IValueHandlerPlugin implementieren).");
            }

            public void Dispose()
            {
                // Schliesst den Scope; PluginCollector.Clear stoppt und disposed die im Scope geladenen
                // Plugins in umgekehrter Ladereihenfolge. Nie das factory-weite ScopeClose verwenden.
                scope?.Dispose();
                scope = null;
            }
        }
    }
}
