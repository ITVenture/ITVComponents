using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Bei <see cref="TokenStatus.Waiting"/> an einem <see cref="Model.CallWorkflowNode"/>: die Id der
        /// Subworkflow-Instanz, auf deren Ende dieser Zweig wartet; sonst null. Endet der Subworkflow,
        /// liefert er ueber diesen Link sein Ergebnis und der Zweig laeuft weiter.
        /// </summary>
        public string WaitingForChildInstanceId { get; set; }

        /// <summary>
        /// Der <b>Zweig-Scope</b>: die eigene Variablen-Kopie dieses Zweigs, oder null, wenn das Token
        /// direkt auf dem Instanz-Scope (<see cref="WorkflowInstance.Variables"/>) arbeitet.
        /// </summary>
        /// <remarks>
        /// Ein AND-Split legt je Strang eine Kopie des Scopes an, den er vorfindet; alles, was der Zweig
        /// danach liest und schreibt, laeuft in dieser Kopie. Damit koennen sich parallele Zweige nicht
        /// mehr gegenseitig ueberschreiben - der Instanz-Scope bleibt waehrend der parallelen Region auf
        /// dem Stand des Splits. Der zugehoerige Join fuehrt die Kopien wieder zusammen
        /// (<see cref="Model.ParallelGatewayNode.Outputs"/>). Null = kein Zweig-Scope: so laufen alle
        /// Tokens ausserhalb paralleler Regionen und alle Instanzen aus der Zeit vor diesem Feld
        /// (unveraendertes Verhalten).
        /// </remarks>
        public Dictionary<string, object> Variables { get; set; }

        /// <summary>
        /// Bei einer wartenden <b>Benutzer-Aufgabe</b> (<see cref="Model.UserActivityNode"/>): der fachliche
        /// Schluessel der Aufgabenart; sonst null. Zugleich das Kennzeichen, dass dieses wartende Token eine
        /// Aufgabe IST - die Aufgabenliste filtert darauf.
        /// </summary>
        /// <remarks>
        /// Die folgenden Aufgaben-Felder sind bewusst am Token <b>festgeschrieben</b> und nicht bei Bedarf
        /// aus der Definition abgeleitet: die Arbeitsliste ist eine Datenbankabfrage ueber viele Instanzen -
        /// sie kann weder Definitions-JSON auspacken noch einen Zuweisungs-Ausdruck auswerten. Alle Felder
        /// werden beim Parken gesetzt und beim Abschluss wieder geleert.
        /// </remarks>
        public string TaskKey { get; set; }

        /// <summary>
        /// Bei einer wartenden Benutzer-Aufgabe: die Permission, die man braucht, um sie zu sehen und zu
        /// erledigen (<see cref="Model.UserActivityNode.RequiredPermission"/>); sonst null.
        /// </summary>
        public string TaskPermission { get; set; }

        /// <summary>
        /// Bei einer wartenden Benutzer-Aufgabe: der Benutzername des Zustaendigen, wie ihn
        /// <see cref="Model.UserActivityNode.Assignment"/> beim Parken geliefert hat. Null = die Aufgabe
        /// gehoert dem Pool.
        /// </summary>
        public string AssignedTo { get; set; }

        /// <summary>
        /// Bei einer wartenden Benutzer-Aufgabe: der Titel fuer die Arbeitsliste - <b>unaufgeloest</b>
        /// (Klartext oder Kultur-JSON). Uebersetzt wird beim Anzeigen, nicht beim Parken: sonst bestimmte
        /// die Kultur des ausfuehrenden Runners die Sprache des Lesers.
        /// </summary>
        public string TaskTitle { get; set; }

        /// <summary>
        /// Bei einer wartenden Benutzer-Aufgabe: wann sie entstanden ist (UTC) - das Sortierkriterium der
        /// Arbeitsliste ("aelteste zuerst").
        /// </summary>
        public DateTime? TaskCreatedUtc { get; set; }

        /// <summary>
        /// Bei einer wartenden Benutzer-Aufgabe: die Frist (UTC), falls
        /// <see cref="Model.UserActivityNode.DueInHours"/> gesetzt ist; sonst null.
        /// </summary>
        /// <remarks>
        /// Bewusst NICHT <see cref="DueUtc"/>: das ist die Timer-Faelligkeit, und der Timer-Aufgriff
        /// (<c>FindDueTimers</c>/<c>ReactivateTimers</c>) wuerde eine ueberfaellige Aufgabe kurzerhand
        /// selbst weiterlaufen lassen. Die Frist ist hier reine Anzeige- und Sortierinformation. Soll sie
        /// etwas ausloesen, haengt am Schritt ein <see cref="Model.BoundaryTimerNode"/> - der bringt sein
        /// EIGENES wartendes Token mit <see cref="DueUtc"/> mit und laesst die Aufgabe in Ruhe.
        /// </remarks>
        public DateTime? TaskDueUtc { get; set; }

        /// <summary>
        /// Die Herkunft dieses Zweigs: die Id des (verbrauchten) Tokens, das den AND-Split ausgefuehrt hat,
        /// aus dem dieses Token hervorgegangen ist; null ausserhalb einer parallelen Region.
        /// </summary>
        /// <remarks>
        /// Das ist die gesamte Zweig-Provenienz: ueber diese Kette findet der Join den Scope, aus dem
        /// gesplittet wurde (der Split-Token wird verbraucht, bleibt aber in der Token-Liste stehen und
        /// traegt seinen Scope weiter) - und damit auch die naechst-aeussere Ebene bei verschachtelten
        /// Splits. Jede Aktivierung eines Splits erzeugt eine neue Token-Id, sodass Wiederholungs-Schleifen
        /// ueber denselben Split nicht miteinander vermischt werden.
        /// </remarks>
        public string SplitTokenId { get; set; }

        /// <summary>
        /// Bei einem Token, das zu einem <see cref="Model.BoundaryTimerNode"/> gehoert: die Id des
        /// HAUPT-Tokens, an dessen Schritt der Timer haengt. Sonst null.
        /// </summary>
        /// <remarks>
        /// Traegt die gesamte Lebensdauer: sowohl das wartende Timer-Token (es steht auf dem Timer-Knoten
        /// und zaehlt ueber <see cref="BoundaryIteration"/> die Ausloesungen) als auch jedes Token des
        /// ausgeloesten Nebenpfads verweisen hierueber auf ihr Haupt-Token. Verlaesst das Haupt-Token
        /// seinen Schritt, werden alle Tokens mit dieser Id verbraucht - Timer wie laufender Nebenpfad.
        /// </remarks>
        public string BoundaryOwnerTokenId { get; set; }

        /// <summary>
        /// Beim wartenden Timer-Token: wie oft bereits ausgeloest wurde (0 = noch nie). Bestimmt, welches
        /// Intervall aus <see cref="Model.BoundaryTimerNode.IntervalsInHours"/> als naechstes gilt.
        /// </summary>
        public int? BoundaryIteration { get; set; }
    }
}
