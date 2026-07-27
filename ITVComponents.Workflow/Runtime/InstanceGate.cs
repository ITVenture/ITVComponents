using System.Collections.Concurrent;

namespace ITVComponents.Workflow.Runtime
{
    /// <summary>
    /// Serialisiert den Zugriff je Instanz: zwei Ausfuehrende duerfen dieselbe Instanz nie
    /// gleichzeitig vorantreiben (sonst zwei Schreiber auf derselben Store-Zeile). Verschiedene
    /// Instanzen laufen nebenlaeufig.
    /// </summary>
    /// <remarks>
    /// Gehoert in den Kern (nicht in die ParallelProcessing-Schicht), weil es die Nebenlaeufigkeits-
    /// Invariante der Engine ausdrueckt und ueber die geteilte <see cref="WorkflowRuntimeContext"/> an
    /// jede Ausfuehrungsschicht weitergereicht wird.
    /// </remarks>
    public sealed class InstanceGate
    {
        private readonly ConcurrentDictionary<string, object> gates = new ConcurrentDictionary<string, object>();

        /// <summary>Liefert das Sperrobjekt fuer eine Instanz.</summary>
        public object For(string instanceId)
        {
            return gates.GetOrAdd(instanceId ?? string.Empty, _ => new object());
        }
    }
}
