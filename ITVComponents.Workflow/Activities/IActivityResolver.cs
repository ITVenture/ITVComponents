using System;
using System.Collections.Generic;

namespace ITVComponents.Workflow.Activities
{
    /// <summary>
    /// Loest den <c>ActivityRef</c> eines automatischen Knotens in eine ausfuehrbare Aktivitaet auf.
    /// </summary>
    /// <remarks>
    /// Diese Abstraktion trennt die Engine von der Herkunft der Aktivitaet. In Phase 0/1 dient eine
    /// einfache In-Memory-Registrierung (<see cref="ActivityRegistry"/>); spaeter tritt ein
    /// Plugin-basierter Resolver an ihre Stelle, der die Aktivitaet on demand aus der PluginFactory
    /// laedt. Gibt der Resolver ein <see cref="IDisposable"/> zurueck, gibt die Engine es nach dem
    /// Schritt frei.
    /// </remarks>
    public interface IActivityResolver
    {
        /// <summary>
        /// Loest den Verweis auf. Wirft, wenn kein passender Eintrag existiert.
        /// </summary>
        /// <param name="activityRef">der Verweis aus dem Knoten</param>
        /// <returns>die Aktivitaet</returns>
        IWorkflowActivity Resolve(string activityRef);
    }

    /// <summary>
    /// Eine einfache, prozessweite Registrierung von Aktivitaeten nach Name. Fuer Tests und feste
    /// Aktivitaeten ohne Plugin-Infrastruktur.
    /// </summary>
    public class ActivityRegistry : IActivityResolver
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
        public IWorkflowActivity Resolve(string activityRef)
        {
            if (activityRef != null && activities.TryGetValue(activityRef, out IWorkflowActivity activity))
            {
                return activity;
            }

            throw new KeyNotFoundException($"No activity registered for '{activityRef}'.");
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
