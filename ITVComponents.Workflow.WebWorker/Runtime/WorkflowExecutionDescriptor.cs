using System;
using System.Collections.Generic;
using System.Threading;

namespace ITVComponents.Workflow.WebWorker.Runtime
{
    /// <summary>
    /// Die (aufgeloeste) Beschreibung EINER Poll-Einheit: eine Umgebung, optional an einen Tenant gebunden,
    /// mit dem Namen der scope-owned <c>WorkflowContext</c>-Dependency (Store) und den Ausfuehrungs-Zielen
    /// (Instanz-Namen) dieser Umgebung. Ergebnis der <see cref="WorkflowEnvironmentDiscovery"/>.
    /// </summary>
    /// <param name="Key">Stabiler, eindeutiger Schluessel (z.B. "env" oder "env|tenant").</param>
    /// <param name="EnvironmentName">Name der Umgebung (fuer Wake-Hook/Diagnose); null = Default-Umgebung.</param>
    /// <param name="TenantId">Tenant-Name (fixierter Scope); null = globales, filterfreies Regime.</param>
    /// <param name="StorePluginName">Name der scope-owned <c>WorkflowContext</c>-Dependency; null = Default-Name.</param>
    /// <param name="HostTargets">Die Ausfuehrungs-Ziele (Instanz-Namen) dieser Umgebung.</param>
    /// <param name="MaxLinger">Poll-Obergrenze dieser Umgebung (Umgebungs-Override vor Worker-Default).</param>
    public sealed record DescriptorSpec(
        string Key,
        string? EnvironmentName,
        string? TenantId,
        string? StorePluginName,
        IReadOnlyList<string> HostTargets,
        TimeSpan MaxLinger);

    /// <summary>
    /// Ein <b>passiver</b> Poll-Deskriptor: haelt nur die aufgeloeste Beschreibung und den Back-off-/
    /// Faelligkeits-Zustand. Kein eigener Thread - der geteilte Pool des <see cref="WorkflowWorkerService"/>
    /// treibt ihn. Der Zustand wird ausschliesslich vom Scheduler (Anspruch) und vom Consumer (Abschluss)
    /// beruehrt; die "running"-CAS serialisiert beide.
    /// </summary>
    public sealed class WorkflowExecutionDescriptor
    {
        private long nextDueUtcTicks;
        private int running;              // 0 = frei, 1 = eingereiht/laufend
        private int pokeRequested;        // 1 = waehrend des Laufs geweckt -> danach heiss halten
        private int initialCleanupDone;   // 0 = verwaiste Locks noch nicht freigegeben (erster Antrieb)
        private volatile bool retired;

        private WorkflowExecutionDescriptor(DescriptorSpec spec)
        {
            Spec = spec;
            // Sofort faellig, damit die erste Runde zuegig laeuft.
            nextDueUtcTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>Die aktuelle (beim Refresh nachgezogene) Beschreibung.</summary>
        public DescriptorSpec Spec { get; private set; }

        public string Key => Spec.Key;

        /// <summary>Erzeugt einen frischen, sofort faelligen Deskriptor aus einer Beschreibung.</summary>
        public static WorkflowExecutionDescriptor FromSpec(DescriptorSpec spec)
            => new WorkflowExecutionDescriptor(spec);

        /// <summary>Zieht eine neue Beschreibung (gleicher Schluessel) nach, behaelt den Back-off-Zustand.</summary>
        public WorkflowExecutionDescriptor WithRefreshedSpec(DescriptorSpec spec)
        {
            Spec = spec;
            return this;
        }

        /// <summary>Markiert den Deskriptor als ausgemustert; ein etwaiger laufender Antrieb darf auslaufen.</summary>
        public void MarkRetired() => retired = true;

        /// <summary>
        /// Liefert true GENAU beim ersten Antrieb dieses Deskriptors - fuer das einmalige Freigeben verwaister
        /// Branch-Locks des (deskriptor-spezifischen) eigenen Owners aus einem frueheren Absturz. Danach false.
        /// </summary>
        public bool TryBeginInitialCleanup() => Interlocked.CompareExchange(ref initialCleanupDone, 1, 0) == 0;

        /// <summary>
        /// Reserviert den Deskriptor fuer einen Lauf, wenn er faellig und frei (und nicht ausgemustert) ist.
        /// Atomar ueber die "running"-CAS.
        /// </summary>
        public bool TryClaimForRun(DateTime nowUtc)
        {
            if (retired)
            {
                return false;
            }

            if (nowUtc.Ticks < Volatile.Read(ref nextDueUtcTicks))
            {
                return false;
            }

            return Interlocked.CompareExchange(ref running, 1, 0) == 0;
        }

        /// <summary>
        /// Weckt den Deskriptor (Wake-Hook): setzt ihn sofort faellig. Passiert das waehrend eines Laufs,
        /// wird er danach heiss gehalten.
        /// </summary>
        public void Poke()
        {
            Volatile.Write(ref nextDueUtcTicks, DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref pokeRequested, 1);
        }

        /// <summary>
        /// Schliesst einen Lauf ab und terminiert den naechsten Poll: bei gefundener Arbeit heiss halten,
        /// sonst exakt auf den naechsten Timer legen (falls bekannt), sonst auf den Max-Linger als
        /// Sicherheitsnetz. Der Wert wird in [now+Min, now+MaxLinger] geklemmt.
        /// </summary>
        public void CompleteRun(bool foundWork, DateTime? nextTimerDueUtc, WorkflowWorkerOptions opt)
        {
            DateTime now = DateTime.UtcNow;
            bool poked = Interlocked.Exchange(ref pokeRequested, 0) == 1;

            DateTime next;
            if (foundWork || poked)
            {
                next = now + opt.MinPollInterval;
            }
            else if (nextTimerDueUtc is DateTime due)
            {
                next = due;
            }
            else
            {
                next = now + Spec.MaxLinger;
            }

            DateTime lower = now + opt.MinPollInterval;
            DateTime upper = now + Spec.MaxLinger;
            if (next < lower)
            {
                next = lower;
            }
            else if (next > upper)
            {
                next = upper;
            }

            Volatile.Write(ref nextDueUtcTicks, next.Ticks);
            // Erst nachdem die Faelligkeit steht, den Deskriptor wieder freigeben (Speicher-Barriere):
            Interlocked.Exchange(ref running, 0);
        }
    }
}
