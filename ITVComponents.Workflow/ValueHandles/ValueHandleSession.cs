using System;
using System.Collections.Generic;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.ValueHandles
{
    /// <summary>
    /// Eine Aufloesungsrunde: alle Griffe, die dabei entstanden sind, und der Plugin-Scope, aus dem
    /// ihre Handler stammen.
    /// </summary>
    /// <remarks>
    /// Der Scope wird <b>traege</b> geoeffnet - erst wenn eine Bindung wirklich einen Handler braucht.
    /// Die weit ueberwiegende Zahl der Knoten hat keine ValueHandle-Bindung, und fuer die soll kein
    /// Plugin-Scope entstehen.
    /// <para>
    /// Die Runde endet mit <see cref="Dispose"/>: danach sind die Griffe tot. Das ist der Grund, warum
    /// eine Benutzer-Aufgabe zweimal aufloest (beim Parken fuer die Maske, beim Abschluss zum
    /// Schreiben) - ein Griff ueberlebt die Persistierung nicht.
    /// </para>
    /// </remarks>
    internal sealed class ValueHandleSession : IDisposable
    {
        /// <summary>
        /// der Host, der Aufloesungs-Kontexte vergibt; null, wenn diese Engine keinen hat
        /// </summary>
        private readonly IValueHandlerHost host;

        /// <summary>
        /// die Instanz, in deren Namen aufgeloest wird
        /// </summary>
        private readonly WorkflowInstance instance;

        /// <summary>
        /// der Zweig, in dem aufgeloest wird, oder null
        /// </summary>
        private readonly string tokenId;

        /// <summary>
        /// die Griffe dieser Runde, in der Reihenfolge ihrer Entstehung
        /// </summary>
        private readonly List<ValueHandleBinding> handles = new List<ValueHandleBinding>();

        /// <summary>
        /// die beim Parken festgeschriebenen Argumente je Eingabeparameter, oder null
        /// </summary>
        private readonly IReadOnlyDictionary<string, Dictionary<string, object>> frozenArguments;

        /// <summary>
        /// der Aufloesungs-Kontext - traege geoeffnet
        /// </summary>
        private IValueHandlerScope scope;

        /// <summary>
        /// die bereits aufgeloesten Handler dieser Runde, je Name
        /// </summary>
        private Dictionary<string, IWorkflowValueHandler> resolved;

        /// <summary>
        /// Initialisiert eine Aufloesungsrunde.
        /// </summary>
        /// <param name="host">der Host, der Aufloesungs-Kontexte vergibt, oder null</param>
        /// <param name="instance">die Instanz, in deren Namen aufgeloest wird</param>
        /// <param name="tokenId">der Zweig, in dem aufgeloest wird, oder null</param>
        /// <param name="frozenArguments">
        /// die beim Parken festgeschriebenen Argumente je Eingabeparameter, oder null. Wo einer vorliegt,
        /// wird er dem Variablen-Stand von jetzt vorgezogen - er benennt den Datensatz, den der Mensch
        /// gesehen hat.
        /// </param>
        public ValueHandleSession(IValueHandlerHost host, WorkflowInstance instance, string tokenId,
            IReadOnlyDictionary<string, Dictionary<string, object>> frozenArguments = null)
        {
            this.host = host;
            this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
            this.tokenId = tokenId;
            this.frozenArguments = frozenArguments;
        }

        /// <summary>Die Griffe dieser Runde.</summary>
        public IReadOnlyList<ValueHandleBinding> Handles => handles;

        /// <summary>
        /// Die beim Parken festgeschriebenen Argumente zu einem Eingabeparameter, oder null.
        /// </summary>
        /// <param name="parameter">der Eingabeparameter</param>
        /// <returns>die festgeschriebenen Argumente, oder null</returns>
        public IReadOnlyDictionary<string, object> FrozenArgumentsFor(string parameter)
        {
            if (frozenArguments == null || parameter == null
                || !frozenArguments.TryGetValue(parameter, out Dictionary<string, object> arguments))
            {
                return null;
            }

            return arguments;
        }

        /// <summary>
        /// Der Griff, der zu einem Eingabeparameter dieser Runde gehoert, oder null.
        /// </summary>
        /// <param name="parameter">der Eingabeparameter</param>
        /// <returns>der Griff, oder null</returns>
        public ValueHandle HandleFor(string parameter)
        {
            foreach (ValueHandleBinding entry in handles)
            {
                if (string.Equals(entry.Binding.Parameter, parameter, StringComparison.Ordinal))
                {
                    return entry.Handle;
                }
            }

            return null;
        }

        /// <summary>
        /// Loest eine ValueHandle-Bindung auf: Handler holen, lesen, Griff bauen.
        /// </summary>
        /// <param name="binding">die Bindung</param>
        /// <param name="nodeId">der Knoten, an dem aufgeloest wird</param>
        /// <param name="arguments">die bereits aufgeloesten Argumente der Bindung</param>
        /// <returns>der gebaute Griff</returns>
        public ValueHandle Resolve(ActivityInputBinding binding, string nodeId,
            IReadOnlyDictionary<string, object> arguments)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            if (string.IsNullOrWhiteSpace(binding.HandlerName))
            {
                throw new InvalidOperationException(
                    $"Input '{binding.Parameter}' of node '{nodeId}' is bound to a value handler, but no " +
                    "handler name is configured.");
            }

            IWorkflowValueHandler handler = ResolveHandler(binding.HandlerName, binding.Parameter, nodeId);
            var request = new ValueHandleRequest(binding.HandlerName, binding.Parameter, arguments,
                instance.Id, instance.DefinitionId, instance.TenantId, tokenId, nodeId);

            object value = handler.Read(request);
            var handle = new ValueHandle(handler, request, value, OnWritten);
            handles.Add(new ValueHandleBinding(binding, handle));
            return handle;
        }

        /// <summary>
        /// Schreibt die Griffe zurueck, deren Bindung das nach erfolgreicher Ausfuehrung verlangt
        /// (<see cref="ValueDelivery.Value"/> und <see cref="ValueWriteBackMode.OnSuccess"/>).
        /// </summary>
        /// <remarks>
        /// Bei <see cref="ValueDelivery.Handle"/> passiert hier nichts: dort hat die Aktivitaet die
        /// volle Kontrolle darueber, was und wann sie schreibt.
        /// </remarks>
        public void WriteBackPending()
        {
            foreach (ValueHandleBinding entry in handles)
            {
                if (entry.Binding.Delivery == ValueDelivery.Value
                    && entry.Binding.WriteBack == ValueWriteBackMode.OnSuccess
                    && !entry.Handle.Written)
                {
                    entry.Handle.WriteBack();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                scope?.Dispose();
            }
            catch (Exception ex)
            {
                // Der Scope gehoert der Runde; sein Ende darf den Vortrieb nicht aufhalten. Verschwiegen
                // wird es trotzdem nicht - ein Handler, der beim Freigeben wirft, haelt sonst still
                // Ressourcen fest, und man sucht die Ursache Wochen spaeter woanders.
                LogEnvironment.LogEvent(
                    $"Value handler scope of instance '{instance.Id}' could not be released: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
            }
            finally
            {
                scope = null;
                resolved = null;
            }
        }

        /// <summary>
        /// Holt den Handler zu einem Namen - je Runde einmal.
        /// </summary>
        /// <param name="handlerName">der konfigurierte Name des Handlers</param>
        /// <param name="parameter">der Parameter, fuer den gefragt wird (fuer die Meldung)</param>
        /// <param name="nodeId">der Knoten, an dem gefragt wird (fuer die Meldung)</param>
        /// <returns>der aufgeloeste Handler</returns>
        private IWorkflowValueHandler ResolveHandler(string handlerName, string parameter, string nodeId)
        {
            if (host == null)
            {
                throw new InvalidOperationException(
                    $"Input '{parameter}' of node '{nodeId}' uses value handler '{handlerName}', but this " +
                    "engine has no value handler host configured.");
            }

            resolved ??= new Dictionary<string, IWorkflowValueHandler>(StringComparer.OrdinalIgnoreCase);
            if (resolved.TryGetValue(handlerName, out IWorkflowValueHandler known))
            {
                return known;
            }

            scope ??= host.OpenScope(instance);
            IWorkflowValueHandler handler = scope.Resolve(handlerName)
                                            ?? throw new InvalidOperationException(
                                                $"Value handler '{handlerName}' of node '{nodeId}' resolved " +
                                                "to nothing.");
            resolved[handlerName] = handler;
            return handler;
        }

        /// <summary>
        /// Haelt jeden Schreibvorgang im Verlauf der Instanz fest - Koordinaten, nicht Werte.
        /// </summary>
        /// <param name="handle">der Griff, der geschrieben wurde</param>
        /// <param name="error">der Fehler, falls das Schreiben scheiterte</param>
        private void OnWritten(ValueHandle handle, Exception error)
        {
            if (error == null)
            {
                instance.Log("ValueHandleWritten", handle.Request.NodeId,
                    $"{handle.Request.HandlerName} -> '{handle.Request.Parameter}'", HistorySeverity.Verbose);
                return;
            }

            instance.Log("ValueHandleWriteFailed", handle.Request.NodeId,
                $"{handle.Request.HandlerName} -> '{handle.Request.Parameter}': {error.Message}");
            LogEnvironment.LogEvent(
                $"Value handle {handle.Request} could not be written: {error.OutlineException()}",
                LogSeverity.Error);
        }
    }

    /// <summary>
    /// Ein Griff samt der Bindung, aus der er entstanden ist.
    /// </summary>
    internal sealed class ValueHandleBinding
    {
        /// <summary>
        /// Initialisiert das Paar.
        /// </summary>
        /// <param name="binding">die Bindung</param>
        /// <param name="handle">der daraus gebaute Griff</param>
        public ValueHandleBinding(ActivityInputBinding binding, ValueHandle handle)
        {
            Binding = binding;
            Handle = handle;
        }

        /// <summary>Die Bindung, aus der der Griff entstanden ist.</summary>
        public ActivityInputBinding Binding { get; }

        /// <summary>Der Griff.</summary>
        public ValueHandle Handle { get; }
    }
}
