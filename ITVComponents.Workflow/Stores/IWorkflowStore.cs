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
        /// Findet Instanzen mit einem wartenden Token auf das angegebene Signal. Ist
        /// <paramref name="correlationKey"/> gesetzt, werden nur Instanzen mit passendem
        /// Korrelationsschluessel (oder passender Id) geliefert.
        /// </summary>
        IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null);

        /// <summary>Findet Instanzen mit einem faelligen Timer-Token (DueUtc &lt;= nowUtc).</summary>
        IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc);

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
        /// Versucht, den Zweig <c>(instanceId, tokenId)</c> fuer den angegebenen Owner (stabiler
        /// Runner-Name) zu sperren - der prozessuebergreifende Ausschluss, damit nicht zwei Runner
        /// denselben Zweig gleichzeitig vorantreiben. Liefert ein Handle bei Erfolg, <c>null</c>, wenn
        /// der Zweig bereits gesperrt ist. Die Sperre hat KEINE TTL (siehe
        /// <see cref="IWorkflowBranchLock"/>).
        /// </summary>
        IWorkflowBranchLock TryAcquireBranchLock(string instanceId, string tokenId, string owner);

        /// <summary>
        /// Gibt alle Sperren des angegebenen Owners frei. Ein Runner ruft das beim Neustart mit seinem
        /// eigenen Namen auf (ein noch gehaltener Lock nach Neustart bedeutet: er ist mittendrin
        /// abgestuerzt) - so werden die betroffenen Zweige sofort wieder frei, ohne Wartefrist.
        /// Dient zugleich als Admin-/Uebernahme-Operation fuer einen endgueltig toten Runner.
        /// </summary>
        void ReleaseLocksOfOwner(string owner);
    }
}
