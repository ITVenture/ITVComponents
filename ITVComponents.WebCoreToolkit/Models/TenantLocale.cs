namespace ITVComponents.WebCoreToolkit.Models
{
    /// <summary>
    /// Die Ortseinstellungen eines Mandanten, wie sie <b>gespeichert</b> sind: Zeitzone und Sprache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Jeder Wert ist der <b>wirksame</b>: ist er am Mandanten selbst leer, gilt der des nächsten übergeordneten
    /// Mandanten, der einen hat (sofern die Umsetzung einen Baum kennt). <c>null</c> heisst: nirgends gesetzt.
    /// </para>
    /// <para>
    /// Die Vorgaben des Hosts (<see cref="System.TimeZoneInfo.Local"/>, Standard-Sprache) kommen hier bewusst
    /// <b>nicht</b> hinein — die kennt das Repository nicht. Das erledigt
    /// <see cref="ITVComponents.WebCoreToolkit.Localization.ITenantLocaleResolver"/>.
    /// </para>
    /// </remarks>
    public class TenantLocale
    {
        /// <summary>Die Id der Zeitzone (<see cref="System.TimeZoneInfo.Id"/>), oder <c>null</c>.</summary>
        public string TimeZone { get; set; }

        /// <summary>Die Kultur, wie gewählt (<c>de-CH</c>, oder nur die Sprache <c>de</c>), oder <c>null</c>.</summary>
        public string Culture { get; set; }

        /// <summary>
        /// Die Zeitzone als Objekt; nirgends gesetzt = die des Servers (<see cref="System.TimeZoneInfo.Local"/>).
        /// </summary>
        /// <remarks>
        /// Eine unbekannte Id wirft (<see cref="System.TimeZoneNotFoundException"/>), wie bisher: ein Tippfehler in
        /// der Zeitzone soll auffallen und nicht still Serverzeit ergeben.
        /// </remarks>
        public System.TimeZoneInfo ResolveTimeZone()
            => string.IsNullOrEmpty(TimeZone) ? System.TimeZoneInfo.Local : System.TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
    }
}
