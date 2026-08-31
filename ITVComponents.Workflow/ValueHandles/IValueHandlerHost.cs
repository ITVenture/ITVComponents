using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;

namespace ITVComponents.Workflow.ValueHandles
{
    /// <summary>
    /// Ein Aufloesungs-Kontext fuer Wert-Handler. Wird je Aufloesungsrunde geoeffnet und danach
    /// wieder geschlossen.
    /// </summary>
    /// <remarks>
    /// Eine Plugin-basierte Implementierung oeffnet darin einen PluginFactory-Scope, laedt die
    /// benannten Handler on demand und gibt sie beim Schliessen frei - dasselbe Muster, mit dem der
    /// Plugin-Aktivitaetskatalog seine Wert-Provider aufloest.
    /// </remarks>
    public interface IValueHandlerScope : IDisposable
    {
        /// <summary>
        /// Loest den Namen einer Bindung in einen Handler auf. Wirft, wenn unter dem Namen nichts
        /// eingerichtet ist oder das Eingerichtete kein <see cref="IWorkflowValueHandler"/> ist.
        /// </summary>
        /// <param name="handlerName">der konfigurierte Name des Handlers</param>
        /// <returns>der aufgeloeste Handler</returns>
        IWorkflowValueHandler Resolve(string handlerName);
    }

    /// <summary>
    /// Vergibt Aufloesungs-Kontexte fuer Wert-Handler.
    /// </summary>
    /// <remarks>
    /// Aufgeloest wird ueber die <b>konfigurierten</b> Namen des Mandanten - dieselbe Latte wie bei
    /// <c>ActivityRef</c>, kein zusaetzliches Gatter. Bei einer <b>oeffentlichen</b> Definition
    /// (<c>TenantId = null</c>) faellt das im Scope des <b>ausfuehrenden</b> Mandanten: derselbe Name
    /// trifft je Mandant, was dort unter ihm eingerichtet ist. Das ist gewollt - und es ist der
    /// Grund, warum ein Handler-Name in einer oeffentlichen Definition eine Aussage ueber die
    /// Einrichtung jedes Mandanten ist.
    /// </remarks>
    public interface IValueHandlerHost
    {
        /// <summary>Oeffnet einen Aufloesungs-Kontext fuer die angegebene Instanz.</summary>
        /// <param name="instance">die Instanz, in deren Namen aufgeloest wird</param>
        /// <returns>der Aufloesungs-Kontext</returns>
        IValueHandlerScope OpenScope(WorkflowInstance instance);
    }

    /// <summary>
    /// Eine einfache, prozessweite Registrierung von Wert-Handlern nach Name (In-Memory, ohne
    /// Plugins). Fuer Tests und feste Handler.
    /// </summary>
    public class ValueHandlerRegistry : IValueHandlerHost
    {
        private readonly Dictionary<string, IWorkflowValueHandler> handlers =
            new Dictionary<string, IWorkflowValueHandler>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registriert einen Handler unter einem Namen.</summary>
        /// <param name="handlerName">der Name, unter dem der Handler ansprechbar ist</param>
        /// <param name="handler">der Handler</param>
        /// <returns>diese Registrierung, fuer Verkettung</returns>
        public ValueHandlerRegistry Register(string handlerName, IWorkflowValueHandler handler)
        {
            handlers[handlerName] = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <inheritdoc/>
        public IValueHandlerScope OpenScope(WorkflowInstance instance)
        {
            return new RegistryScope(this);
        }

        private IWorkflowValueHandler Resolve(string handlerName)
        {
            if (handlerName != null && handlers.TryGetValue(handlerName, out IWorkflowValueHandler handler))
            {
                return handler;
            }

            throw new KeyNotFoundException($"No value handler registered for '{handlerName}'.");
        }

        private sealed class RegistryScope : IValueHandlerScope
        {
            private readonly ValueHandlerRegistry owner;

            public RegistryScope(ValueHandlerRegistry owner)
            {
                this.owner = owner;
            }

            public IWorkflowValueHandler Resolve(string handlerName)
            {
                return owner.Resolve(handlerName);
            }

            public void Dispose()
            {
                // Registrierte Handler gehoeren der Registry - nichts freizugeben.
            }
        }
    }
}
