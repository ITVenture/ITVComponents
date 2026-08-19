using System;
using System.Collections.Generic;

namespace ITVComponents.Workflow.Instances
{
    /// <summary>
    /// Eine Nachricht, die eine Instanz senden will - <b>vorgemerkt im selben Commit</b> wie der Zweig,
    /// der sie ausgeloest hat.
    /// </summary>
    /// <remarks>
    /// Das ist der Kern der Zustell-Garantie. Ohne diese Vormerkung lebt eine ausstehende Zustellung nur
    /// im Speicher: stirbt der Prozess zwischen dem Commit des Senders und dem Zustellen, ist die
    /// Nachricht ersatzlos weg, und niemand erfaehrt davon. Als Zeile in derselben Transaktion ueberlebt
    /// sie den Absturz, und ein Runner holt sie nach.
    /// <para>
    /// Der Preis ist <b>mindestens einmal</b> statt genau einmal: stirbt der Prozess zwischen dem
    /// Zustellen und dem Loeschen der Vormerkung, wird beim naechsten Aufgriff erneut zugestellt. Das ist
    /// die bewusste Wahl - eine verlorene Nachricht ist der teurere Fehler als eine doppelte, und gegen
    /// die doppelte kann sich der Empfaenger wehren (Korrelation + eigene Idempotenz).
    /// </para>
    /// </remarks>
    public class OutgoingMessage
    {
        /// <summary>Eindeutige Kennung dieser Vormerkung.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Die Instanz, die sendet. Wird vom Ablageort gesetzt.</summary>
        public string InstanceId { get; set; }

        /// <summary>Der Signalname.</summary>
        public string SignalName { get; set; }

        /// <summary>Der Korrelationsschluessel bei einer gerichteten Nachricht; sonst null.</summary>
        public string CorrelationKey { get; set; }

        /// <summary>Ob es ein Rundruf ist (dann ohne Korrelation).</summary>
        public bool Broadcast { get; set; }

        /// <summary>
        /// Die Zielinstanz, wenn ausdruecklich EINE gemeint ist (<c>SignalWorkflow</c>); sonst null.
        /// </summary>
        public string TargetInstanceId { get; set; }

        /// <summary>Die Nutzdaten, oder null.</summary>
        public Dictionary<string, object> Payload { get; set; }

        /// <summary>
        /// Das Token des Senders, das auf die Zustellung <b>wartet</b>, oder null (senden und weiter).
        /// </summary>
        public string WaitingTokenId { get; set; }

        /// <summary>
        /// Bei einem wartenden Sender: die Variable, in die die Zahl der erreichten Empfaenger geschrieben
        /// wird; sonst null.
        /// </summary>
        public string ReachedVariable { get; set; }

        /// <summary>
        /// Der Mandant, aus dem die Nachricht <b>stammt</b> - bei einer vorgemerkten Nachricht der der
        /// sendenden Instanz. Null = kein Ursprung bekannt.
        /// </summary>
        /// <remarks>
        /// Er entscheidet, welche Definitionen die Nachricht <b>anlaufen</b> laesst (siehe
        /// <c>IWorkflowStore.FindMessageTriggers</c>) - nicht, wer sie empfaengt: die Zustellung an
        /// wartende Instanzen laeuft unveraendert ueber Name und Korrelation. Ohne ihn eroeffnete eine
        /// einzige Nachricht in jedem Mandanten, dessen Definition auf den Namen horcht, einen Vorgang.
        /// <para>
        /// Er muss die Vormerkung ueberleben: die Zustellung kann in einem anderen Prozess nachgeholt
        /// werden, und dort ist der sendende Mandant sonst nicht mehr zu ermitteln.
        /// </para>
        /// </remarks>
        public string OriginTenantId { get; set; }

        /// <summary>Wann die Nachricht vorgemerkt wurde (UTC).</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Wie oft die Zustellung bereits versucht wurde.</summary>
        public int Attempts { get; set; }
    }
}
