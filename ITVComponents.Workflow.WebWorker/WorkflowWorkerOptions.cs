using System;

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
    }
}
