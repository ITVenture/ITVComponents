using System;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Wie ausfuehrlich eine Vorlage die Zugriffe auf ihre Freigaben protokolliert.
    /// </summary>
    public enum AssetAuditMode
    {
        /// <summary>Gar nicht.</summary>
        Off = 0,

        /// <summary>
        /// Nur die Verweigerungen. Die guenstigste Einstellung - und die Haelfte, die man hinterher
        /// braucht.
        /// </summary>
        DeniedOnly = 1,

        /// <summary>
        /// Einstiege und Verweigerungen. Vorgabe: eine Freigabe ist etwas, das man aus der Hand gibt -
        /// ob sie benutzt wurde, ist genau die Frage, die spaeter gestellt wird.
        /// </summary>
        All = 2
    }

    /// <summary>
    /// Ein Zugriff auf eine Freigabe.
    /// </summary>
    public sealed class AssetAccessEntry
    {
        /// <summary>Die Freigabe, oder null bei einem Ad-hoc-Ticket.</summary>
        public string AssetKey { get; set; }

        /// <summary>Die Kennung des Tickets, oder null bei einer gespeicherten Freigabe.</summary>
        public string TicketNonce { get; set; }

        /// <summary>Die Vorlage - sie ist das Einzige, was beide Faelle gemeinsam haben.</summary>
        public string TemplateSystemKey { get; set; }

        /// <summary>Der Mandant, dem die Freigabe gehoert.</summary>
        public string TenantName { get; set; }

        /// <summary>An wen sie gerichtet war. Eine Behauptung, kein Nachweis.</summary>
        public string RecipientLabel { get; set; }

        /// <summary>Unter welchem Namen der Zugreifende auftrat.</summary>
        public string AccessedBy { get; set; }

        /// <summary>Der angefragte Pfad, in kanonischer Form.</summary>
        public string RequestPath { get; set; }

        /// <summary>Worauf die Freigabe zeigt, lesbar zusammengefasst.</summary>
        public string ArgumentSummary { get; set; }

        /// <summary>Ob der Zugriff durchgelassen wurde.</summary>
        public bool Granted { get; set; }

        /// <summary>
        /// Kurz und maschinenlesbar, warum nicht - <c>NotConfirmed</c>, <c>Denied</c>. Der ausfuehrliche
        /// Grund steht im Log.
        /// </summary>
        public string DenyReason { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Schreibt Zugriffe auf Freigaben mit.
    /// <para>
    /// <b>Aufgerufen wird je Vorgang, nicht je Anfrage.</b> Der Riegel am Ausgang ist genau die Stelle,
    /// an der ein Vorgang beginnt - Unterressourcen einer Seite laufen dort nicht durch. Wuerde man
    /// jede Anfrage mitschreiben, waere das Protokoll nach einer Woche unbenutzbar; dieselbe Lektion
    /// hat der SystemLog schon einmal erteilt.
    /// </para>
    /// <para>
    /// <b>Verweigerungen gehoeren dazu.</b> Sie sind die Haelfte, die man braucht, wenn jemand fragt,
    /// warum ein Link nicht funktioniert hat - oder ob jemand an etwas herangewollt hat, das ihm nicht
    /// gehoert.
    /// </para>
    /// </summary>
    public interface IAssetAccessLog
    {
        /// <summary>
        /// Schreibt einen Zugriff mit, sofern die Vorlage es verlangt.
        /// </summary>
        /// <param name="entry">der Zugriff</param>
        void Record(AssetAccessEntry entry);
    }
}
