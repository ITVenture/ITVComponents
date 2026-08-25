using System;
using ITVComponents.Workflow.Retention;

namespace ITVComponents.Workflow.WebWorker
{
    /// <summary>
    /// Stellschrauben des Workflow-Background-Workers. Alle Werte gelten prozessweit; der Max-Linger ist
    /// zusaetzlich pro Umgebung ueberschreibbar (<c>WorkflowEnvironment.MaxLingerSeconds</c>).
    /// </summary>
    public sealed class WorkflowWorkerOptions
    {
        /// <summary>
        /// Maximale Anzahl paralleler Antriebe im GANZEN Prozess (geteilter Pool), nicht pro Deskriptor.
        /// </summary>
        public int MaxConcurrency { get; set; } = 12;

        /// <summary>Heisses Poll-Intervall, wenn ein Deskriptor zuletzt Arbeit gefunden hat (Burst leeren).</summary>
        public TimeSpan MinPollInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Globaler Default-Max-Linger: die Poll-Obergrenze, auf die ein leerlaufender Deskriptor OHNE bekannte
        /// Faelligkeit hochlaeuft (Sicherheitsnetz fuer Crash-Recovery/Handoff). Pro Umgebung ueberschreibbar.
        /// </summary>
        public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>Wie oft die (teure) Tenant/Umgebungs-Discovery neu abgeglichen wird.</summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>Wie oft der Scheduler die Faelligkeit der Deskriptoren prueft (kurzer, guenstiger Tick).</summary>
        public TimeSpan SchedulerTick { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Synthetischer Hintergrund-Benutzer fuer <c>PrepareBackgroundContext</c> (muss NICHT in der DB
        /// existieren). Macht die tenant-abhaengigen Query-Filter im Antriebs-Scope wirksam.
        /// </summary>
        public string BackgroundUserName { get; set; } = "#Toolkit#Process";

        /// <summary>
        /// Stabiler Owner-Name fuer die prozessuebergreifenden Branch-Locks. Beim Neustart gibt der Worker die
        /// Locks dieses Owners frei (ein gehaltener Lock nach Neustart = mitten im Lauf abgestuerzt).
        /// </summary>
        public string LockOwnerName { get; set; } = "WorkflowWebWorker";

        /// <summary>
        /// Wie lange ein aufgegriffener faelliger Timer fuer diesen Worker reserviert bleibt. Muss
        /// laenger sein als ein Timer-Antrieb dauert; laenger als noetig verzoegert nur die Uebernahme
        /// durch einen anderen Prozess, falls dieser hier mittendrin abstuerzt.
        /// </summary>
        public TimeSpan TimerLease { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Wie viele Instanzen mit faelligen Timern ein Antrieb hoechstens aufgreift. Der Rest bleibt
        /// fuer den naechsten Durchgang oder einen anderen Prozess liegen.
        /// </summary>
        public int MaxTimerBatch { get; set; } = 200;

        /// <summary>
        /// Wie oft der <b>Aufbewahrungslauf</b> faehrt. <b>Vorgabe <see cref="TimeSpan.Zero"/> = aus.</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Ausdruecklich abgeschaltet, bis jemand es einschaltet. Das ist der einzige Lauf hier, der
        /// Daten <b>loescht</b>; einen solchen von selbst mitlaufen zu lassen, weil ein Paket
        /// aktualisiert wurde, waere die falsche Vorgabe - auch wenn ohne eingestellte Fristen ohnehin
        /// nichts passierte. Ein sinnvoller Wert sind Stunden, nicht Minuten.
        /// </para>
        /// <para>
        /// Der Lauf faehrt <b>je Umgebung</b> und nicht je Deskriptor: er raeumt
        /// mandantenuebergreifend, und bei mandantengebundenen Deskriptoren taete sonst jeder dieselbe
        /// Arbeit. Dieselbe Lehre wie beim Poll-Deskriptor.
        /// </para></remarks>
        public TimeSpan RetentionInterval { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Die globalen Aufbewahrungs-Vorgaben - die letzte Stufe der Kette hinter dem Widerspruch des
        /// Mandanten und der Vorgabe der Definition. Null = keine, dann gilt nur, was an einer
        /// Definition steht.
        /// </summary>
        public WorkflowRetentionDefaults? RetentionDefaults { get; set; }

        /// <summary>
        /// Wie viele Prozessbaeume (bzw. Vorgaenge beim Anhang-Lauf) je Definition, Mandant und Lauf
        /// hoechstens verarbeitet werden. Der Rest bleibt fuer den naechsten Durchgang liegen - und der
        /// Lauf sagt, dass er gedeckelt hat.
        /// </summary>
        public int MaxRetentionBatch { get; set; } = 200;
    }
}
