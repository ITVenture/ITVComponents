using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;

namespace ITVComponents.Workflow.Activities
{
    /// <summary>
    /// Ein Aufloesungs-Kontext fuer die Dauer eines Vortriebs (eines Advance-Laufs) einer Instanz.
    /// Aktivitaeten werden hierueber aufgeloest; beim Schliessen werden die dabei bezogenen
    /// Ressourcen freigegeben.
    /// </summary>
    /// <remarks>
    /// So oeffnet eine Plugin-basierte Implementierung einen PluginFactory-Scope je Vortrieb, laedt
    /// die benoetigten Schritt-Plugins darin on demand und disposed sie beim Schliessen wieder -
    /// waehrend eine Instanz wartet, haelt sie keine Plugin-Ressourcen. Die In-Memory-Registrierung
    /// braucht nichts freizugeben und schliesst folgenlos.
    /// </remarks>
    public interface IActivityScope : IDisposable
    {
        /// <summary>
        /// Loest den <c>ActivityRef</c> eines Knotens in eine ausfuehrbare Aktivitaet auf. Wirft,
        /// wenn kein passender Eintrag existiert.
        /// </summary>
        IWorkflowActivity Resolve(string activityRef);
    }

    /// <summary>
    /// Vergibt Aufloesungs-Kontexte fuer Aktivitaeten. Die Engine oeffnet je Vortrieb einer Instanz
    /// genau einen Scope.
    /// </summary>
    /// <remarks>
    /// Trennt die Engine von der Herkunft der Aktivitaeten. In Tests und einfachen Szenarien dient
    /// die In-Memory-<see cref="ActivityRegistry"/>; ein Plugin-basierter Host laedt die Aktivitaet
    /// je Schritt on demand aus der PluginFactory.
    /// </remarks>
    public interface IActivityHost
    {
        /// <summary>Oeffnet einen Aufloesungs-Kontext fuer den Vortrieb der angegebenen Instanz.</summary>
        IActivityScope OpenScope(WorkflowInstance instance);
    }

    /// <summary>
    /// Eine einfache, prozessweite Registrierung von Aktivitaeten nach Name (In-Memory, ohne
    /// Plugins). Fuer Tests und feste Aktivitaeten.
    /// </summary>
    public class ActivityRegistry : IActivityHost
    {
        private readonly Dictionary<string, IWorkflowActivity> activities =
            new Dictionary<string, IWorkflowActivity>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registriert eine Aktivitaet unter einem Namen.</summary>
        public ActivityRegistry Register(string activityRef, IWorkflowActivity activity)
        {
            activities[activityRef] = activity ?? throw new ArgumentNullException(nameof(activity));
            return this;
        }

        /// <summary>Registriert eine Aktivitaet als Delegat.</summary>
        public ActivityRegistry Register(string activityRef, Action<WorkflowActivityContext> action)
        {
            return Register(activityRef, new DelegateActivity(action));
        }

        /// <inheritdoc/>
        public IActivityScope OpenScope(WorkflowInstance instance)
        {
            return new RegistryScope(this);
        }

        private IWorkflowActivity Resolve(string activityRef)
        {
            if (activityRef != null && activities.TryGetValue(activityRef, out IWorkflowActivity activity))
            {
                return activity;
            }

            throw new KeyNotFoundException($"No activity registered for '{activityRef}'.");
        }

        private sealed class RegistryScope : IActivityScope
        {
            private readonly ActivityRegistry owner;

            public RegistryScope(ActivityRegistry owner)
            {
                this.owner = owner;
            }

            public IWorkflowActivity Resolve(string activityRef)
            {
                return owner.Resolve(activityRef);
            }

            public void Dispose()
            {
                // Registrierte Aktivitaeten gehoeren der Registry - nichts freizugeben.
            }
        }

        private sealed class DelegateActivity : IWorkflowActivity
        {
            private readonly Action<WorkflowActivityContext> action;

            public DelegateActivity(Action<WorkflowActivityContext> action)
            {
                this.action = action ?? throw new ArgumentNullException(nameof(action));
            }

            public void Execute(WorkflowActivityContext context)
            {
                action(context);
            }
        }
    }
}
