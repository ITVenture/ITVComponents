using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.ValueHandles;

namespace ITVComponents.Workflow.Activities
{
    /// <summary>
    /// Ein Aufloesungs-Kontext fuer die Dauer einer Arbeitseinheit an einer Instanz: ein Vortrieb, ein
    /// Zweig-Task, oder das Beschreiben bzw. Abschliessen einer Benutzer-Aufgabe. Aktivitaeten
    /// <b>und</b> Wert-Handler werden hierueber aufgeloest; beim Schliessen werden die dabei bezogenen
    /// Ressourcen freigegeben.
    /// </summary>
    /// <remarks>
    /// So oeffnet eine Plugin-basierte Implementierung einen PluginFactory-Scope je Arbeitseinheit,
    /// laedt die benoetigten Plugins darin on demand und disposed sie beim Schliessen wieder -
    /// waehrend eine Instanz wartet, haelt sie keine Plugin-Ressourcen. Die In-Memory-Registrierung
    /// braucht nichts freizugeben und schliesst folgenlos.
    /// <para>
    /// <b>Warum Schritte und Wert-Handler denselben Scope teilen:</b> beide holen Daten im Namen
    /// derselben Instanz, unter demselben Mandanten. Getrennte Scopes hiessen zwei Plugin-Factorys und
    /// zwei DB-Kontexte fuer einen Knoten - und bei einer <c>Delivery = Value</c>-Bindung mutiert die
    /// Aktivitaet dann ein Objekt, das an einem fremden Kontext haengt.
    /// </para>
    /// </remarks>
    public interface IActivityScope : IDisposable
    {
        /// <summary>
        /// Loest den <c>ActivityRef</c> eines Knotens in eine ausfuehrbare Aktivitaet auf. Wirft,
        /// wenn kein passender Eintrag existiert.
        /// </summary>
        IWorkflowActivity Resolve(string activityRef);

        /// <summary>
        /// Loest den <c>HandlerName</c> einer <see cref="Model.ParameterBindingKind.ValueHandle"/>-Bindung
        /// in einen Wert-Handler auf. Wirft, wenn unter dem Namen nichts eingerichtet ist oder das
        /// Eingerichtete kein <see cref="IWorkflowValueHandler"/> ist.
        /// </summary>
        /// <remarks>
        /// Aufgeloest wird ueber die <b>konfigurierten</b> Namen des ausfuehrenden Mandanten - dieselbe
        /// Latte wie beim <c>ActivityRef</c>, kein zusaetzliches Gatter. Bei einer <b>oeffentlichen</b>
        /// Definition (<c>TenantId = null</c>) faellt das im Scope des ausfuehrenden Mandanten: derselbe
        /// Name trifft je Mandant, was dort unter ihm eingerichtet ist. Das ist gewollt - und es ist der
        /// Grund, warum ein Handler-Name in einer oeffentlichen Definition eine Aussage ueber die
        /// Einrichtung jedes Mandanten ist.
        /// </remarks>
        /// <param name="handlerName">der konfigurierte Name des Handlers</param>
        /// <returns>der aufgeloeste Handler</returns>
        IWorkflowValueHandler ResolveValueHandler(string handlerName);
    }

    /// <summary>
    /// Vergibt Aufloesungs-Kontexte fuer Aktivitaeten und Wert-Handler. Die Engine oeffnet je
    /// Arbeitseinheit an einer Instanz genau einen Scope.
    /// </summary>
    /// <remarks>
    /// Trennt die Engine von der Herkunft der Aktivitaeten und Handler. In Tests und einfachen
    /// Szenarien dient die In-Memory-<see cref="ActivityRegistry"/>; ein Plugin-basierter Host laedt
    /// on demand aus der PluginFactory.
    /// <para>
    /// Eine Arbeitseinheit ist: ein Vortrieb (<c>Advance</c>), ein Zweig-Task (<c>RunBranch</c>), das
    /// Beschreiben einer Benutzer-Aufgabe (<c>DescribeUserTask</c>) oder deren Abschluss
    /// (<c>CompleteUserTask</c>). Die beiden letzten kommen aus der Oberflaeche und treiben nichts
    /// voran - sie brauchen den Scope fuer die Wert-Handler der Maske.
    /// </para>
    /// </remarks>
    public interface IActivityHost
    {
        /// <summary>
        /// Oeffnet einen Aufloesungs-Kontext fuer eine Arbeitseinheit an der angegebenen Instanz.
        /// </summary>
        IActivityScope OpenScope(WorkflowInstance instance);
    }

    /// <summary>
    /// Eine einfache, prozessweite Registrierung von Aktivitaeten und Wert-Handlern nach Name
    /// (In-Memory, ohne Plugins). Fuer Tests und feste Einrichtungen.
    /// </summary>
    /// <remarks>
    /// Die hier registrierten Objekte sind <b>prozessweite Singletons</b>: sie werden einmal abgelegt
    /// und von jeder Instanz, jedem Mandanten und jedem Thread geteilt. Fuer eine Aktivitaet oder einen
    /// Wert-Handler, der nichts festhaelt und seine Kontexte je Aufruf leiht, ist das richtig; alles
    /// andere gehoert an einen Plugin-basierten Host, der je Arbeitseinheit frisch laedt.
    /// </remarks>
    public class ActivityRegistry : IActivityHost
    {
        private readonly Dictionary<string, IWorkflowActivity> activities =
            new Dictionary<string, IWorkflowActivity>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, IWorkflowValueHandler> valueHandlers =
            new Dictionary<string, IWorkflowValueHandler>(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>Registriert einen Wert-Handler unter einem Namen.</summary>
        /// <param name="handlerName">der Name, unter dem der Handler ansprechbar ist</param>
        /// <param name="handler">der Handler</param>
        /// <returns>diese Registrierung, fuer Verkettung</returns>
        public ActivityRegistry RegisterValueHandler(string handlerName, IWorkflowValueHandler handler)
        {
            valueHandlers[handlerName] = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
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

        private IWorkflowValueHandler ResolveValueHandler(string handlerName)
        {
            if (handlerName != null
                && valueHandlers.TryGetValue(handlerName, out IWorkflowValueHandler handler))
            {
                return handler;
            }

            throw new KeyNotFoundException($"No value handler registered for '{handlerName}'.");
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

            public IWorkflowValueHandler ResolveValueHandler(string handlerName)
            {
                return owner.ResolveValueHandler(handlerName);
            }

            public void Dispose()
            {
                // Registrierte Objekte gehoeren der Registry - nichts freizugeben.
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
