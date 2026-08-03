using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Stores
{
    /// <summary>
    /// Persistiert Workflow-Definitionen und -Instanzen und beantwortet die Abfragen, die die
    /// Engine zum Wiederaufnehmen braucht.
    /// </summary>
    /// <remarks>
    /// Bewusst abstrahiert: die Engine haengt nur an diesem Vertrag. Standard-Implementierung wird
    /// ein DB-Store (EFRepo); die In-Memory-Variante dient Tests und einfachen Szenarien. Die
    /// Abfragen <see cref="FindWaitingForSignal"/> und <see cref="FindDueTimers"/> sind der Grund,
    /// warum der Store mehr koennen muss als blosses Ablegen von Bytes.
    /// </remarks>
    public interface IWorkflowStore
    {
        /// <summary>Legt eine Definition ab (Upsert nach Id+Version).</summary>
        void SaveDefinition(WorkflowDefinition definition);

        /// <summary>
        /// Laedt eine Definition. Ist <paramref name="version"/> null, wird die hoechste Version
        /// geliefert. Liefert null, wenn nichts gefunden wird.
        /// </summary>
        WorkflowDefinition GetDefinition(string definitionId, int? version = null);

        /// <summary>Legt eine Instanz ab (Upsert nach Id). Setzt den Aenderungszeitpunkt.</summary>
        void SaveInstance(WorkflowInstance instance);

        /// <summary>
        /// Speichert die Instanz nur, wenn ihr Stand in der Datenbank noch <paramref name="baseVersion"/>
        /// entspricht (optimistische Nebenlaeufigkeit). Liefert true bei Erfolg (und erhoeht die Version),
        /// false bei einem Versionskonflikt (ein anderer Zweig hat inzwischen committed). Bei false laedt
        /// der Aufrufer neu und wendet sein Delta erneut an - die Aktivitaet selbst wird dabei NICHT erneut
        /// ausgefuehrt. So serialisieren sich gleichzeitige Zweig-Merges derselben Instanz kurz, waehrend
        /// die eigentliche (lange) Ausfuehrung parallel bleibt.
        /// </summary>
        bool TryCommitInstance(WorkflowInstance instance, int baseVersion);

        /// <summary>Laedt eine Instanz, oder null.</summary>
        WorkflowInstance GetInstance(string instanceId);

        /// <summary>
        /// Liefert nur die Dringlichkeit einer Instanz (<see cref="WorkflowInstance.Priority"/>), oder
        /// null, wenn es sie nicht gibt.
        /// </summary>
        /// <remarks>
        /// Eigene Abfrage, weil die Ausfuehrungsschicht den Wert braucht, um einen Auftrag einzureihen -
        /// und dafuer nicht Variablen, Tokens und das ganze Protokoll der Instanz laden soll. Genau dafuer
        /// steht die Prioritaet als eigene Spalte neben dem JSON.
        /// </remarks>
        int? GetInstancePriority(string instanceId);

        /// <summary>
        /// Findet Instanzen mit einem wartenden Token auf das angegebene Signal. Ist
        /// <paramref name="correlationKey"/> gesetzt, werden nur Instanzen geliefert, bei denen der
        /// Schluessel passt - entweder am <b>Wartepunkt</b> (<c>Token.WaitingCorrelation</c>) oder an der
        /// Instanz (Korrelationsschluessel oder Id).
        /// </summary>
        /// <remarks>
        /// Der Schluessel am Wartepunkt ist der spezifischere: er entsteht erst beim Warten und kann
        /// deshalb auf etwas zeigen, das der Prozess selbst gerade erzeugt hat. Eine Instanz kann an
        /// mehreren Stellen auf verschiedene Schluessel warten - deswegen reicht der Filter an der Instanz
        /// allein nicht.
        /// </remarks>
        IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null);

        /// <summary>
        /// Findet Instanzen mit einem wartenden Token, das einen <b>Rundruf</b> dieses Namens erwartet
        /// (<c>Token.WaitingKind == WaitKind.Signal</c>) - ohne Korrelation.
        /// </summary>
        /// <remarks>
        /// Eigene Abfrage statt eines Filters am Ergebnis von <see cref="FindWaitingForSignal"/>: ein
        /// Rundruf kann tausende Instanzen betreffen, und die Auswahl gehoert in die Datenbank. Sonst
        /// muesste fuer jede Instanz erst ihre Definition geladen werden, nur um die Art des Wartepunkts
        /// zu erfahren.
        /// </remarks>
        IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string signalName);

        /// <summary>Findet Instanzen mit einem faelligen Timer-Token (DueUtc &lt;= nowUtc).</summary>
        /// <remarks>
        /// Reines Lesen - jeder Aufrufer sieht jeden faelligen Timer. Fuer den Aufgriff durch einen
        /// Runner ist <see cref="ClaimDueTimers"/> gedacht; diese Abfrage bleibt fuer Diagnose und
        /// Tests.
        /// </remarks>
        IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc);

        /// <summary>
        /// Nimmt faellige Timer fuer sich in Anspruch und liefert die zugehoerigen Instanzen. Anders als
        /// <see cref="FindDueTimers"/> ist das kein blosses Lesen: die aufgegriffenen Timer werden fuer
        /// die Dauer von <paramref name="lease"/> auf <paramref name="owner"/> gestempelt und sind fuer
        /// andere Runner so lange unsichtbar.
        /// </summary>
        /// <remarks>
        /// Der Anspruch dient der <b>Last</b>, nicht der Korrektheit. Dass ein Timer genau einmal
        /// feuert, sichert weiterhin allein der Versions-Check beim Commit
        /// (<c>WorkflowEngine.ReactivateTimers</c>) - er muss es auch, denn ein Anspruch kann ablaufen,
        /// waehrend sein Halter noch arbeitet. Was der Anspruch verhindert, ist, dass N Runner dieselben
        /// Instanzen laden, um danach N-1 mal am Commit zu scheitern. Genau deshalb darf ein
        /// <b>abgelaufener</b> Anspruch gefahrlos uebernommen werden - ein abgestuerzter Runner haelt
        /// nichts fest.
        /// <para>
        /// Ein Store ohne verteilte Sicht (In-Memory) darf schlicht die faelligen Instanzen liefern.
        /// </para>
        /// </remarks>
        /// <param name="nowUtc">der Jetzt-Zeitpunkt</param>
        /// <param name="owner">
        /// der aufgreifende Runner - derselbe stabile Name wie bei den Zweig-Sperren, damit
        /// <see cref="ReleaseLocksOfOwner"/> beim Neustart auch seine Ansprueche mit abraeumt
        /// </param>
        /// <param name="lease">wie lange der Anspruch gilt</param>
        /// <param name="maxInstances">
        /// Obergrenze je Aufruf; der Rest bleibt fuer den naechsten Poll oder einen anderen Runner
        /// liegen. Ohne sie riesse bei einem Stau der erste Runner alles an sich.
        /// </param>
        /// <returns>die Instanzen, deren faellige Timer dieser Aufruf bekommen hat</returns>
        IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime nowUtc, string owner, TimeSpan lease,
            int maxInstances);

        /// <summary>
        /// Liefert die frueheste NOCH NICHT faellige Timer-Faelligkeit (DueUtc &gt; nowUtc) im Sichtbereich des
        /// Stores, oder null, wenn kein Timer aussteht. Erlaubt einem Background-Worker, den naechsten Poll
        /// exakt auf den naechsten Timer zu legen, statt blind zu pollen.
        /// </summary>
        DateTime? PeekNextTimerDueUtc(DateTime nowUtc);

        /// <summary>
        /// Findet Instanzen mit einem Zweig, der auf eines der angegebenen Ausfuehrungs-Ziele wartet
        /// (Token-Status <see cref="TokenStatus.WaitingForTarget"/> mit passendem
        /// <see cref="Token.WaitingTarget"/>). Damit nimmt ein Runner die Zweige auf, die auf genau die von
        /// ihm bedienten Ziele warten (verteilter Handoff). Eine leere Zielmenge liefert nichts.
        /// </summary>
        IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets);

        /// <summary>
        /// Findet lauffaehige Instanzen (Status <see cref="WorkflowStatus.Running"/>). Damit nimmt
        /// ein startender Dienst Instanzen wieder auf, deren Vortrieb - etwa durch einen Absturz
        /// mitten im Lauf - liegengeblieben ist.
        /// </summary>
        IEnumerable<WorkflowInstance> FindRunnable();

        /// <summary>
        /// Findet die direkten Kind-Instanzen (Subworkflows) der angegebenen Eltern-Instanz. Dient dem
        /// Abbruch einer ganzen Prozess-Kaskade und dem Vortrieb eines Prozessbaums im einfachen Betrieb.
        /// </summary>
        IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId);

        /// <summary>
        /// Findet beendete (Completed/Faulted) Subworkflow-Instanzen, auf die noch ein Eltern-Token wartet.
        /// Damit holt ein Runner eine Ergebnis-Zustellung nach, die im schmalen Fenster zwischen
        /// Kind-Abschluss und Eltern-Benachrichtigung (z.B. durch einen Absturz) liegen geblieben ist.
        /// </summary>
        IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent();

        /// <summary>
        /// Versucht, den Zweig <c>(instanceId, tokenId)</c> fuer den angegebenen Owner (stabiler
        /// Runner-Name) zu sperren - der prozessuebergreifende Ausschluss, damit nicht zwei Runner
        /// denselben Zweig gleichzeitig vorantreiben. Liefert ein Handle bei Erfolg, <c>null</c>, wenn
        /// der Zweig bereits gesperrt ist. Die Sperre hat KEINE TTL (siehe
        /// <see cref="IWorkflowBranchLock"/>).
        /// </summary>
        IWorkflowBranchLock TryAcquireBranchLock(string instanceId, string tokenId, string owner);

        /// <summary>
        /// Gibt alle Sperren des angegebenen Owners frei - Zweig-Sperren und Timer-Ansprueche
        /// (<see cref="ClaimDueTimers"/>). Ein Runner ruft das beim Neustart mit seinem eigenen Namen
        /// auf (ein noch gehaltener Lock nach Neustart bedeutet: er ist mittendrin abgestuerzt) - so
        /// werden die betroffenen Zweige sofort wieder frei, ohne Wartefrist. Dient zugleich als
        /// Admin-/Uebernahme-Operation fuer einen endgueltig toten Runner.
        /// </summary>
        /// <remarks>
        /// Fuer die Timer-Ansprueche ist das reine Beschleunigung: die laufen ohnehin ab. Ohne diesen
        /// Aufruf staende ein neu gestarteter Runner aber bis zum Ablauf vor seinen eigenen, verwaisten
        /// Anspruechen - und zwar genau in dem Moment, in dem er die liegengebliebene Arbeit aufholen soll.
        /// </remarks>
        void ReleaseLocksOfOwner(string owner);
    }
}
