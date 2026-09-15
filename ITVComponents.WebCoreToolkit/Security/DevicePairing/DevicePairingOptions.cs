using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Security.DevicePairing
{
    /// <summary>
    /// Die Stellschrauben des Kopplungs-Ablaufs.
    /// </summary>
    [SettingName("DevicePairing")]
    public class DevicePairingOptions
    {
        /// <summary>
        /// Wie lange ein offener Vorgang gilt, in Minuten.
        /// </summary>
        /// <remarks>
        /// <b>Ablauf ist Pflicht, nicht Option</b> - ein Kopplungsvorgang, der offen bleibt, ist ein
        /// dauerhaft gueltiger Einstieg. Zehn Minuten reichen fuer "Code vorlesen, abtippen, bestaetigen"
        /// und sind kurz genug, dass ein vergessener Vorgang von selbst verschwindet.
        /// </remarks>
        public int LifetimeMinutes { get; set; } = 10;

        /// <summary>
        /// Wie oft das Geraet fruehestens nachfragen darf, in Sekunden.
        /// </summary>
        /// <remarks>
        /// Die halbe Bremse: ohne sie waere der kurze Benutzercode ratbar. Die andere Haelfte ist
        /// <see cref="MaxPolls"/>.
        /// </remarks>
        public int PollIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Wie oft ein Vorgang insgesamt abgefragt werden darf, bevor er abgelehnt wird.
        /// </summary>
        /// <remarks>
        /// Bei der Vorgabe reicht das fuer die volle Lebensdauer bei eingehaltenem Intervall, mit
        /// reichlich Luft. Wer haeufiger fragt, versucht etwas anderes.
        /// </remarks>
        public int MaxPolls { get; set; } = 240;

        /// <summary>
        /// Laenge des Benutzercodes (ohne den Trenner).
        /// </summary>
        public int UserCodeLength { get; set; } = 8;

        /// <summary>
        /// Das Recht, das zum Bestaetigen einer Kopplung noetig ist.
        /// </summary>
        /// <remarks>
        /// Wer koppeln darf, ist eine Entscheidung des Hosts. Die Vorgabe passt zu den uebrigen
        /// App-Rechten (<c>Apps.Templates.Write</c>, <c>Apps.PermissionSets.Write</c>).
        /// </remarks>
        public string ConfirmPermission { get; set; } = "Apps.Pairing.Confirm";
    }
}
