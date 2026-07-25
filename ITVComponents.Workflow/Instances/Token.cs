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

        /// <summary>
        /// An einem parallelen Join geparkt: das Token ist angekommen und wartet, bis auf jeder
        /// eingehenden Kante ein Token liegt. Anders als <see cref="Waiting"/> ist das ein internes
        /// Warten auf Geschwister-Tokens, nicht auf ein aeusseres Ereignis.
        /// </summary>
        Joining,

        /// <summary>
        /// Auf ein Ausfuehrungs-Ziel wartend: das Token steht auf einem Aktivitaets-Knoten, dessen
        /// <see cref="Model.AutomatedActivityNode.ExecutionTarget"/> der aktuelle Runner nicht bedienen
        /// kann. Es ruht (wie <see cref="Waiting"/>), bis ein Runner mit passendem Ziel den Zweig aufnimmt
        /// und dort ausfuehrt (verteilter Handoff). Der Zielname steht in
        /// <see cref="Token.WaitingTarget"/>.
        /// </summary>
        WaitingForTarget,

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

        /// <summary>
        /// Bei <see cref="TokenStatus.WaitingForTarget"/>: der Name des Ausfuehrungs-Ziels, auf das der
        /// Zweig wartet (der <see cref="Model.AutomatedActivityNode.ExecutionTarget"/> des Knotens, auf dem
        /// das Token steht). Ein Runner, dessen Ziele diesen Namen enthalten, nimmt den Zweig auf. Sonst
        /// null.
        /// </summary>
        public string WaitingTarget { get; set; }
    }
}
