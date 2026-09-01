using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.ValueHandles;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Loest automatische Schritte und Wert-Handler als Plugins aus der PluginFactory auf. Je
    /// Arbeitseinheit an einer Instanz wird ein Scope geoeffnet, die benoetigten Plugins werden darin on
    /// demand geladen und beim Schliessen des Scopes wieder freigegeben.
    /// </summary>
    /// <remarks>
    /// Die uebergebene <see cref="PluginFactory"/> sollte mit <see cref="ScopeMode.PerAsyncContext"/>
    /// erzeugt sein - die Engine kann Schritte auf ThreadPool-/ParallelProcessing-Workern
    /// ausfuehren, und nur so ueberlebt der Scope die Ausfuehrungsgrenzen sauber.
    ///
    /// Der Konstruktions-String eines Schritts wird NICHT hier hinterlegt: die Factory loest den
    /// <c>ActivityRef</c> im Scope ueber ihren <see cref="ITVComponents.Plugins.Initialization.IDynamicLoader"/>
    /// selbst auf (dessen Scoped-Plugin-Definition traegt den Konstruktions-String, ueblicherweise
    /// datenbankgetrieben) und laedt das Plugin autonom - genau das leistet <c>scope[name, true]</c>.
    /// </remarks>
    public sealed class PluginActivityHost : IActivityHost
    {
        private readonly PluginFactory factory;

        /// <summary>
        /// Initialisiert den Host mit der Factory, aus deren Scope die Schritt-Plugins geladen werden.
        /// </summary>
        public PluginActivityHost(PluginFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc/>
        public IActivityScope OpenScope(WorkflowInstance instance)
        {
            return new PluginActivityScope(factory, instance);
        }

        /// <summary>
        /// Ein Aufloesungs-Kontext fuer eine Arbeitseinheit. Oeffnet den PluginFactory-Scope traege beim
        /// ersten aufgeloesten Plugin und schliesst ihn (samt der geladenen Plugins) beim Dispose.
        /// </summary>
        private sealed class PluginActivityScope : IActivityScope
        {
            private readonly PluginFactory factory;
            private readonly WorkflowInstance instance;

            private IPluginFactory scope;

            public PluginActivityScope(PluginFactory factory, WorkflowInstance instance)
            {
                this.factory = factory;
                this.instance = instance;
            }

            public IWorkflowActivity Resolve(string activityRef)
            {
                // scope[name, true]: die Factory loest den ActivityRef ueber den IDynamicLoader des
                // Scopes selbst auf (Konstruktions-String aus dessen Scoped-Plugin-Definition) und laedt
                // das Plugin in den Scope-Collector - der cached pro Name, ein erneutes Resolve in
                // derselben Arbeitseinheit liefert also dieselbe Instanz, und der Scope gibt sie beim
                // Dispose frei.
                if (Scope[activityRef, true] is IActivityPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den ActivityRef '{activityRef}' konnte kein Workflow-Aktivitaets-Plugin " +
                    "aufgeloest werden. Es muss ein Scoped-Plugin dieses Namens ueber einen IDynamicLoader " +
                    "der Factory bereitstehen (und IActivityPlugin implementieren).");
            }

            public IWorkflowValueHandler ResolveValueHandler(string handlerName)
            {
                // Derselbe Scope wie fuer die Schritte - siehe IActivityScope: der Handler holt fremde
                // Daten im Namen derselben Instanz und soll dabei an denselben Kontexten haengen wie die
                // Aktivitaet, die den Wert danach benutzt.
                if (Scope[handlerName, true] is IValueHandlerPlugin plugin)
                {
                    return plugin;
                }

                throw new InvalidOperationException(
                    $"Fuer den Handler-Namen '{handlerName}' konnte kein Workflow-Wert-Handler aufgeloest " +
                    "werden. Es muss ein Scoped-Plugin dieses Namens ueber einen IDynamicLoader der Factory " +
                    "bereitstehen (und IValueHandlerPlugin implementieren).");
            }

            /// <summary>
            /// Der PluginFactory-Scope dieser Arbeitseinheit - traege geoeffnet.
            /// </summary>
            /// <remarks>
            /// Erst beim ersten aufgeloesten Plugin: eine Arbeitseinheit, die weder Aktivitaet noch
            /// Wert-Handler braucht, zahlt nichts. <c>transientLoadingScope: false</c>, damit die
            /// geladenen Plugins beim Schliessen disposed werden.
            /// </remarks>
            private IPluginFactory Scope => scope ??= factory.NewScope(
                new Dictionary<string, object> { { "instanceId", instance.Id } },
                null,
                false);

            public void Dispose()
            {
                // Schliesst den Scope; PluginCollector.Clear stoppt und disposed die im Scope
                // geladenen Plugins in umgekehrter Ladereihenfolge. Nie das factory-weite ScopeClose
                // verwenden - gezielt diesen Scope schliessen.
                scope?.Dispose();
                scope = null;
            }
        }
    }
}
