namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Optionen fuer die Registry der Asset-Argument-Konsumenten.
    /// </summary>
    public class AssetArgumentRegistryOptions
    {
        /// <summary>
        /// Ob die Deklarationen gespeichert werden. Vorgabe <b>an</b> - anders als bei der
        /// Auto-Berechtigungs-Registrierung entstehen hier nur Deklarationen, die nichts gewaehren. Aus
        /// gedreht bleibt die Registry im Speicher, und die Maske weiss nur so viel wie die laufende
        /// Instanz.
        /// </summary>
        public bool Persist { get; set; } = true;

        /// <summary>
        /// Wie lange nach einer Meldung gewartet wird, bevor der Hintergrund-Worker schreibt. Der Start
        /// einer Anwendung meldet in Schueben; das Warten fasst sie zu wenigen Schreibvorgaengen zusammen.
        /// </summary>
        public int DebounceMilliseconds { get; set; } = 250;

        /// <summary>
        /// Wie alt der gespeicherte Zeitstempel einer <b>unveraenderten</b> Deklaration werden darf, bevor
        /// er nachgefuehrt wird. Ihn bei jedem Seitenaufbau anzufassen waere genau der Schreibsturm, den
        /// die Warteschlange vermeiden soll - und fuer die Frage "ist diese Zeile noch aktuell" genuegt
        /// Tagesgenauigkeit.
        /// </summary>
        public int TouchIntervalHours { get; set; } = 24;
    }
}
