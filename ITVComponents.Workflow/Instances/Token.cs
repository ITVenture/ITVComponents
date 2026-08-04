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
        /// Bei einem Zweig aus einem <b>inklusiven Gateway</b>: wie viele Zweige dieser Split aktiviert
        /// hat. Sonst null.
        /// </summary>
        /// <remarks>
        /// Das ist die ganze Idee des strukturierten OR: WIE VIELE kommen, weiss allein der Split - er hat
        /// die Bedingungen ausgewertet. Der Join zaehlt dann nur noch, statt die (nicht entscheidbare)
        /// Frage zu beantworten, ob ihn noch irgendein Token erreichen kann. Der Wert reist mit dem Zweig
        /// mit, weil er von den Laufzeitwerten abhaengt und aus dem Modell nicht ableitbar ist.
        /// </remarks>
        public int? SplitBranchCount { get; set; }

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

        /// <summary>
        /// Ueber welche Kante dieses Token an seinem aktuellen Knoten angekommen ist; null bei einem
        /// Start-Token (und bei Tokens aus der Zeit vor diesem Feld).
        /// </summary>
        /// <remarks>
        /// Gebraucht vom <b>Join</b>: er feuert, wenn JEDE eingehende Kante geliefert hat - nicht, wenn
        /// die blosse Anzahl wartender Tokens stimmt. Ohne die Kante liesse sich ein unbalancierter Graph
        /// (zwei Tokens ueber dieselbe Kante, eine andere leer) nicht von einem vollstaendigen Join
        /// unterscheiden, und der Join feuerte mit halber Mannschaft.
        /// </remarks>
        public string ArrivedViaFlowId { get; set; }

        /// <summary>
        /// Bei einem Token INNERHALB eines <see cref="Model.SubProcessNode"/>: die Id des aeusseren
        /// Tokens, das am Subprozess-Knoten wartet. Sonst null.
        /// </summary>
        /// <remarks>
        /// Traegt die Zugehoerigkeit ueber den ganzen Innenraum: daran erkennt die Engine, wann der
        /// Subprozess fertig ist (kein lebendes Token mehr mit dieser Id) und welche Tokens beim
        /// Abbruch - etwa durch einen Fristen-Timer am Subprozess - mit wegzuraeumen sind.
        /// </remarks>
        public string SubProcessOwnerTokenId { get; set; }

        /// <summary>
        /// Bei einem Token, das gerade einen <b>Rueckabwicklungs-Pfad</b> laeuft: die Id des Tokens, das
        /// am <see cref="Model.CompensateNode"/> darauf wartet. Sonst null.
        /// </summary>
        /// <remarks>
        /// Erreicht dieses Token sein Nebenpfad-Ende, weiss die Engine darueber, wen sie wecken muss -
        /// und dass jetzt der naechste vorgemerkte Schritt an der Reihe ist. Genau das macht die
        /// Rueckabwicklung SEQUENZIELL, ohne dafuer eine eigene Ablaufsteuerung zu brauchen.
        /// </remarks>
        public string CompensationOwnerTokenId { get; set; }

        /// <summary>
        /// Bei einem Token, das an einem <see cref="Model.EventGatewayNode"/> um die Wette wartet: die Id
        /// des (verbrauchten) Gateway-Tokens. Alle Geschwister desselben Rennens tragen denselben Wert;
        /// sobald eines weiterlaeuft, werden die uebrigen verbraucht. Sonst null.
        /// </summary>
        /// <remarks>
        /// Beim Gewinner wird der Wert geloescht, sobald er seinen Wartepunkt verlaesst - danach ist er
        /// ein ganz gewoehnliches Token. Ohne dieses Loeschen wuerde ein spaeteres Rennen mit derselben
        /// Gateway-Id (Wiederholungs-Schleife) alte Geschwister mit einbeziehen.
        /// </remarks>
        public string RaceTokenId { get; set; }

        /// <summary>
        /// Bei einem an einem <see cref="Model.WaitNode"/> wartenden Token: der aufgeloeste
        /// Korrelationsschluessel dieses Wartepunkts (aus
        /// <see cref="Model.WaitNode.CorrelationExpression"/>), oder null.
        /// </summary>
        /// <remarks>
        /// Am TOKEN und nicht an der Instanz, weil er beim Warten entsteht und nicht beim Anlegen: eine
        /// Instanz kann an mehreren Stellen auf verschiedene Schluessel warten. Ist er null, gilt der
        /// Korrelationsschluessel der Instanz (oder ihre Id) - das bisherige Verhalten.
        /// </remarks>
        public string WaitingCorrelation { get; set; }

        /// <summary>
        /// Bei einem an einem <see cref="Model.WaitNode"/> wartenden Token: ob es eine gerichtete
        /// Nachricht oder einen Rundruf erwartet (<see cref="Model.WaitKind"/>). Null bei allen anderen
        /// Wartearten.
        /// </summary>
        /// <remarks>
        /// Am Token gefuehrt, damit die Auswahl der Empfaenger in der DATENBANK stattfinden kann: ein
        /// Rundruf muss alle passenden Tokens finden, ohne fuer jede Instanz erst ihre Definition zu
        /// laden.
        /// </remarks>
        public Model.WaitKind? WaitingKind { get; set; }

        /// <summary>
        /// Uebernimmt den GESAMTEN Zustand eines anderen Tokens (alles ausser <see cref="Id"/>).
        /// </summary>
        /// <remarks>
        /// Diese Methode - und nicht der Aufrufer - kennt die Feldliste. Der Zweig-Commit kopiert und
        /// vergleicht Tokens an mehreren Stellen (Schnappschuss, Diff, Merge); wird dort je eine eigene
        /// Feldliste gefuehrt, faellt beim naechsten neuen Feld eine davon durch, und der Fehler ist nicht
        /// zu sehen, sondern nur zu merken: die Spalte bleibt in der Datenbank leer. Genau so verschwanden
        /// Benutzer-Aufgaben aus der Arbeitsliste (Stempel im Diff, aber nicht im Merge).
        /// </remarks>
        /// <param name="source">das Token, dessen Zustand uebernommen wird</param>
        public void CopyStateFrom(Token source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            NodeId = source.NodeId;
            Status = source.Status;
            WaitingSignal = source.WaitingSignal;
            DueUtc = source.DueUtc;
            WaitingTarget = source.WaitingTarget;
            WaitingForChildInstanceId = source.WaitingForChildInstanceId;
            // Der Zweig-Scope gehoert dem Zweig: eine eigene Kopie, damit Quelle und Ziel nicht auf
            // demselben Stack stehen (sonst waere ein Diff dagegen immer leer).
            Variables = CopyScope(source.Variables);
            SplitTokenId = source.SplitTokenId;
            SplitBranchCount = source.SplitBranchCount;
            BoundaryOwnerTokenId = source.BoundaryOwnerTokenId;
            BoundaryIteration = source.BoundaryIteration;
            RaceTokenId = source.RaceTokenId;
            WaitingCorrelation = source.WaitingCorrelation;
            WaitingKind = source.WaitingKind;
            ArrivedViaFlowId = source.ArrivedViaFlowId;
            SubProcessOwnerTokenId = source.SubProcessOwnerTokenId;
            CompensationOwnerTokenId = source.CompensationOwnerTokenId;
            TaskKey = source.TaskKey;
            TaskPermission = source.TaskPermission;
            AssignedTo = source.AssignedTo;
            TaskTitle = source.TaskTitle;
            TaskCreatedUtc = source.TaskCreatedUtc;
            TaskDueUtc = source.TaskDueUtc;
        }

        /// <summary>Ein neues Token mit derselben <see cref="Id"/> und einer eigenen Kopie des Zustands.</summary>
        public Token CloneState()
        {
            var copy = new Token { Id = Id };
            copy.CopyStateFrom(this);
            return copy;
        }

        /// <summary>
        /// Tragen beide Tokens denselben Zustand (alles ausser <see cref="Id"/>)? Der Gegenpart zu
        /// <see cref="CopyStateFrom"/> - beide Feldlisten muessen deckungsgleich bleiben, sonst meldet ein
        /// Diff eine Aenderung nicht, die der Merge uebertragen wuerde.
        /// </summary>
        public static bool SameState(Token a, Token b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return a.NodeId == b.NodeId && a.Status == b.Status && a.WaitingSignal == b.WaitingSignal
                   && Nullable.Equals(a.DueUtc, b.DueUtc) && a.WaitingTarget == b.WaitingTarget
                   && a.WaitingForChildInstanceId == b.WaitingForChildInstanceId
                   && SameScope(a.Variables, b.Variables)
                   && a.SplitTokenId == b.SplitTokenId
                   && Nullable.Equals(a.SplitBranchCount, b.SplitBranchCount)
                   && a.BoundaryOwnerTokenId == b.BoundaryOwnerTokenId
                   && Nullable.Equals(a.BoundaryIteration, b.BoundaryIteration)
                   && a.RaceTokenId == b.RaceTokenId
                   && a.WaitingCorrelation == b.WaitingCorrelation
                   && Nullable.Equals(a.WaitingKind, b.WaitingKind)
                   && a.ArrivedViaFlowId == b.ArrivedViaFlowId
                   && a.SubProcessOwnerTokenId == b.SubProcessOwnerTokenId
                   && a.CompensationOwnerTokenId == b.CompensationOwnerTokenId
                   && a.TaskKey == b.TaskKey && a.TaskPermission == b.TaskPermission
                   && a.AssignedTo == b.AssignedTo && a.TaskTitle == b.TaskTitle
                   && Nullable.Equals(a.TaskCreatedUtc, b.TaskCreatedUtc)
                   && Nullable.Equals(a.TaskDueUtc, b.TaskDueUtc);
        }

        /// <summary>Eine flache Kopie eines Zweig-Scopes, oder null.</summary>
        private static Dictionary<string, object> CopyScope(Dictionary<string, object> scope)
            => scope == null ? null : new Dictionary<string, object>(scope, StringComparer.Ordinal);

        /// <summary>Vergleicht zwei Zweig-Scopes flach (Schluessel und Werte).</summary>
        private static bool SameScope(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, object> kv in a)
            {
                if (!b.TryGetValue(kv.Key, out object other) || !Equals(kv.Value, other))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
