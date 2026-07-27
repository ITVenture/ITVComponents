using System;

namespace ITVComponents.Workflow.Stores
{
    /// <summary>
    /// Ein Handle auf eine gehaltene Zweig-Sperre. Solange es lebt, darf nur der besitzende Runner den
    /// Zweig (Instanz + Token) vorantreiben. <see cref="IDisposable.Dispose"/> gibt die Sperre frei.
    /// </summary>
    /// <remarks>
    /// Die Sperre hat bewusst <b>keine TTL</b>: sie gilt, bis sie freigegeben oder ueber
    /// <see cref="IWorkflowStore.ReleaseLocksOfOwner"/> zurueckgesetzt wird. So werden auch sehr lange
    /// Aktivitaeten nie „aus Ungeduld" abgebrochen. Der Preis dafuer ist die Neustart-getriebene
    /// Wiederherstellung (ein Runner raeumt beim Hochfahren seine eigenen alten Sperren ab).
    /// </remarks>
    public interface IWorkflowBranchLock : IDisposable
    {
        /// <summary>Die gesperrte Instanz.</summary>
        string InstanceId { get; }

        /// <summary>Das gesperrte Token (der Zweig) innerhalb der Instanz.</summary>
        string TokenId { get; }

        /// <summary>Der Besitzer der Sperre (stabiler Runner-Name).</summary>
        string Owner { get; }
    }
}
