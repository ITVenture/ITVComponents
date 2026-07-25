using System;
using System.Threading;

namespace ITVComponents.Workflow.Runtime
{
    /// <summary>
    /// Der ambiente Ausfuehrungs-Kontext eines Workflow-Vortriebs. Traegt den Tenant, unter dem die
    /// gerade vorangetriebene Instanz laeuft - so kann ein <b>tenant-uebergreifender</b> Hintergrund-
    /// Runner jede Instanz unter IHREM Tenant abarbeiten, ohne dass tenant-abhaengige Aktivitaeten oder
    /// Datenbank-Kontexte von einem HTTP-Benutzer abhaengen muessten.
    /// </summary>
    /// <remarks>
    /// Umgesetzt ueber <see cref="AsyncLocal{T}"/>: der Wert gilt fuer den aktuellen Ausfuehrungsfluss.
    /// Der Vortrieb einer Instanz laeuft synchron auf einem Worker, und die dabei geladenen Aktivitaeten
    /// sehen denselben Wert. Im Web-Betrieb setzt niemand den Scope -> <see cref="HasTenant"/> ist false
    /// und tenant-abhaengige Kontexte greifen wie bisher auf ihren injizierten Benutzer-/Security-Kontext
    /// zu (der ambiente Wert lebt pro Ausfuehrungsfluss und leckt daher nicht in Web-Anfrage-Threads).
    ///
    /// Der Store des Runners bleibt bewusst <b>filterfrei</b> (er muss laufende Instanzen aller Tenants
    /// finden); dieser Scope steuert das per-Instanz-Verhalten waehrend des Vortriebs, nicht die
    /// tenant-uebergreifende Suche.
    /// </remarks>
    public static class WorkflowExecutionScope
    {
        private static readonly AsyncLocal<TenantHolder> current = new AsyncLocal<TenantHolder>();

        /// <summary>
        /// Gibt an, ob gerade ein Tenant-Kontext gesetzt ist. Unterscheidet „bewusst tenant-frei"
        /// (gesetzt auf null) von „kein Scope aktiv".
        /// </summary>
        public static bool HasTenant => current.Value != null;

        /// <summary>Der aktuell gesetzte Tenant, oder null (auch wenn kein Scope aktiv ist).</summary>
        public static string CurrentTenant => current.Value?.Tenant;

        /// <summary>
        /// Setzt den Tenant fuer den aktuellen Ausfuehrungsfluss. Der vorige Wert wird beim
        /// <see cref="IDisposable.Dispose"/> des Rueckgabewerts wiederhergestellt (verschachtelbar).
        /// </summary>
        /// <param name="tenant">der Tenant (null = bewusst tenant-frei)</param>
        /// <returns>ein Scope-Handle; beim Dispose wird der vorige Zustand wiederhergestellt</returns>
        public static IDisposable UseTenant(string tenant)
        {
            TenantHolder previous = current.Value;
            current.Value = new TenantHolder(tenant);
            return new Restore(previous);
        }

        private sealed class TenantHolder
        {
            public TenantHolder(string tenant)
            {
                Tenant = tenant;
            }

            public string Tenant { get; }
        }

        private sealed class Restore : IDisposable
        {
            private readonly TenantHolder previous;
            private bool done;

            public Restore(TenantHolder previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (!done)
                {
                    current.Value = previous;
                    done = true;
                }
            }
        }
    }
}
