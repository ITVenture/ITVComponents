using System;

namespace ITVComponents.Workflow.Instances
{
    /// <summary>
    /// Der Zustand eines Tokens.
    /// </summary>
    public enum TokenStatus
    {
        /// <summary>Aktiv: das Token steht auf einem Knoten und wartet auf Verarbeitung.</summary>
        Active,

        /// <summary>Wartend: das Token haengt an einem Wartepunkt (Signal oder Timer).</summary>
        Waiting,

        /// <summary>Verbraucht: das Token hat einen Endpunkt erreicht oder ist in einem Join aufgegangen.</summary>
        Consumed
    }

    /// <summary>
    /// Eine Marke, die eine aktive Position im Workflow-Graphen markiert. Mehrere gleichzeitig
    /// aktive Tokens einer Instanz bilden parallele Zweige ab.
    /// </summary>
    /// <remarks>
    /// Das Token traegt keinen Verhaltenscode - nur, wo es steht und worauf es ggf. wartet. Dadurch
    /// ist es (wie die ganze Instanz) reine, serialisierbare Zustandsdaten.
    /// </remarks>
    public class Token
    {
        /// <summary>Innerhalb der Instanz eindeutige Kennung des Tokens.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Id des Knotens, auf dem das Token gerade steht.</summary>
        public string NodeId { get; set; }

        /// <summary>Der aktuelle Zustand des Tokens.</summary>
        public TokenStatus Status { get; set; } = TokenStatus.Active;

        /// <summary>
        /// Bei <see cref="TokenStatus.Waiting"/> an einem Signal-Wartepunkt: der Name des erwarteten
        /// Signals; sonst null.
        /// </summary>
        public string WaitingSignal { get; set; }

        /// <summary>
        /// Bei <see cref="TokenStatus.Waiting"/> an einem Timer: der Faelligkeitszeitpunkt (UTC);
        /// sonst null.
        /// </summary>
        public DateTime? DueUtc { get; set; }
    }
}
