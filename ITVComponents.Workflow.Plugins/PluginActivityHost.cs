using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Loest automatische Schritte als Plugins aus der PluginFactory auf. Je Vortrieb einer Instanz
    /// wird ein Scope geoeffnet, die benoetigten Schritt-Plugins werden darin on demand geladen und
    /// beim Schliessen des Scopes wieder freigegeben.
    /// </summary>
    /// <remarks>
    /// Die uebergebene <see cref="PluginFactory"/> sollte mit <see cref="ScopeMode.PerAsyncContext"/>
    /// erzeugt sein - die Engine kann Schritte auf ThreadPool-/ParallelProcessing-Workern
    /// ausfuehren, und nur so ueberlebt der Scope die Ausfuehrungsgrenzen sauber.
    ///
    /// Der <paramref name="resolveConstructor"/> bildet den logischen <c>ActivityRef</c> eines
    /// Knotens auf einen Plugin-Konstruktions-String <c>[Assembly]&lt;Typ&gt;Parameter</c> ab.
    /// </remarks>
    public sealed class PluginActivityHost : IActivityHost
    {
        private readonly PluginFactory factory;
        private readonly Func<string, string> resolveConstructor;

        /// <summary>
        /// Initialisiert den Host mit einer Aufloesungsfunktion ActivityRef -&gt; Konstruktions-String.
        /// </summary>
        public PluginActivityHost(PluginFactory factory, Func<string, string> resolveConstructor)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.resolveConstructor = resolveConstructor ?? throw new ArgumentNullException(nameof(resolveConstructor));
        }

        /// <summary>
        /// Initialisiert den Host mit einer festen Zuordnung ActivityRef -&gt; Konstruktions-String.
        /// </summary>
        public PluginActivityHost(PluginFactory factory, IReadOnlyDictionary<string, string> constructionStrings)
            : this(factory, MakeResolver(constructionStrings))
        {
        }

        /// <inheritdoc/>
        public IActivityScope OpenScope(WorkflowInstance instance)
        {
            return new PluginActivityScope(factory, resolveConstructor, instance);
        }

        private static Func<string, string> MakeResolver(IReadOnlyDictionary<string, string> constructionStrings)
        {
            if (constructionStrings == null)
            {
                throw new ArgumentNullException(nameof(constructionStrings));
            }

            return activityRef => constructionStrings.TryGetValue(activityRef, out string ctor)
                ? ctor
                : throw new KeyNotFoundException($"No activity plugin mapped for '{activityRef}'.");
        }

        /// <summary>
        /// Ein Aufloesungs-Kontext fuer einen Vortrieb. Oeffnet den PluginFactory-Scope traege beim
        /// ersten aufgeloesten Schritt und schliesst ihn (samt der geladenen Plugins) beim Dispose.
        /// </summary>
        private sealed class PluginActivityScope : IActivityScope
        {
            private readonly PluginFactory factory;
            private readonly Func<string, string> resolveConstructor;
            private readonly WorkflowInstance instance;
            private readonly Dictionary<string, IWorkflowActivity> loaded =
                new Dictionary<string, IWorkflowActivity>(StringComparer.Ordinal);

            private IPluginFactory scope;

            public PluginActivityScope(PluginFactory factory, Func<string, string> resolveConstructor,
                WorkflowInstance instance)
            {
                this.factory = factory;
                this.resolveConstructor = resolveConstructor;
                this.instance = instance;
            }

            public IWorkflowActivity Resolve(string activityRef)
            {
                // Innerhalb eines Vortriebs wird dieselbe Aktivitaet nur einmal geladen und
                // wiederverwendet - der Kontext kommt ohnehin je Aufruf ueber Execute.
                if (loaded.TryGetValue(activityRef, out IWorkflowActivity existing))
                {
                    return existing;
                }

                // Scope erst jetzt oeffnen: ein Vortrieb ohne Aktivitaet zahlt nichts.
                // transientLoadingScope: false, damit die geladenen Plugins beim Schliessen disposed
                // werden.
                scope ??= factory.NewScope(
                    new Dictionary<string, object> { { "instanceId", instance.Id } },
                    null,
                    false);

                string constructor = resolveConstructor(activityRef);
                IActivityPlugin plugin = scope.LoadPlugin<IActivityPlugin>(activityRef, constructor);
                loaded[activityRef] = plugin;
                return plugin;
            }

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
