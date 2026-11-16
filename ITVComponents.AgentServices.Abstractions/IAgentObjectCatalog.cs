namespace ITVComponents.AgentServices.Abstractions
{
    /// <summary>
    /// Feste Namen zwischen Agent und Web-Anwendung.
    /// </summary>
    public static class AgentCatalog
    {
        /// <summary>
        /// Der Objektname, unter dem der Agent seinen <see cref="IAgentObjectCatalog"/> bereitstellt, wenn
        /// das Template der ClientApp keinen anderen nennt.
        /// </summary>
        public const string DefaultObjectName = "AgentCatalog";

        /// <summary>
        /// Der Konstruktor-Parameter, über den ein Gerät seine lokal abgelegten Einstellungen bekommt:
        /// <c>&lt;MeinGeraet&gt;$DeviceSettings</c>.
        /// </summary>
        public const string DeviceSettingsParameterName = "DeviceSettings";
    }

    /// <summary>
    /// Was der Agent an Geräten anbietet, wie sie sich einrichten lassen, und die Einrichtung selbst.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Läuft am Agenten und wird von der Web-Anwendung über den Dienst-Verteiler angesprochen (Dienst =
    /// <c>ClientKey</c>, Objekt = <see cref="AgentCatalog.DefaultObjectName"/> oder der Name aus dem Template).
    /// </para>
    /// <para>
    /// <b>Kein Aufruf baut ein Gerät.</b> Aufzählen und Beschreiben laufen über den Typ; die Auswahlwerte
    /// kommen von eigenen <see cref="IDeviceChoiceProvider"/>n.
    /// </para>
    /// <para>
    /// Rückgaben sind Arrays: über die Interprozess-Kommunikation muss der Laufzeittyp eines
    /// Rückgabewerts ein Array sein.
    /// </para>
    /// </remarks>
    public interface IAgentObjectCatalog
    {
        /// <summary>Die Geräte, die einen Vertrag erfüllen.</summary>
        /// <param name="contractTypeName">der volle Name des Vertrags (<see cref="Type.FullName"/>)</param>
        /// <returns>die Geräte; leer, wenn keines den Vertrag erfüllt — das ist kein Fehler</returns>
        Task<AgentObjectInfo[]> GetObjectsAsync(string contractTypeName);

        /// <summary>Die Einstellungen, die ein Gerät zulässt.</summary>
        /// <param name="objectName">der Name des Geräts</param>
        /// <returns>die Felder; leer, wenn das Gerät keine Einstellungen hat</returns>
        Task<SettingDescriptor[]> DescribeSettingsAsync(string objectName);

        /// <summary>Die Auswahl für ein Feld, die nur der Rechner vor Ort kennt.</summary>
        /// <param name="objectName">der Name des Geräts</param>
        /// <param name="settingName">das Feld</param>
        /// <param name="draftJson">die Werte, die in der Maske gerade stehen, als JSON-Objekt</param>
        Task<SettingChoice[]> GetChoicesAsync(string objectName, string settingName, string? draftJson);

        /// <summary>Die Einstellungen, die am Agenten für ein Gerät abgelegt sind, als JSON.</summary>
        /// <param name="objectName">der Name des Geräts</param>
        /// <returns>das JSON, oder <c>null</c>, wenn das Gerät noch nicht eingerichtet ist</returns>
        Task<string?> GetSettingsAsync(string objectName);

        /// <summary>Prüft Einstellungen gegen den Einstellungstyp des Geräts und legt sie lokal ab.</summary>
        /// <param name="objectName">der Name des Geräts</param>
        /// <param name="settingsJson">die Werte als JSON-Objekt</param>
        /// <returns>das Ergebnis; bei Fehlern je Feld eine Meldung, nichts wurde abgelegt</returns>
        Task<ApplySettingsResult> ApplySettingsAsync(string objectName, string settingsJson);
    }

    /// <summary>Ein Gerät, das der Agent anbietet.</summary>
    public class AgentObjectInfo
    {
        /// <summary>Der Name, unter dem ein Proxy darauf entsteht — der Wert, der gespeichert wird.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Wie es in einer Auswahl heissen soll; leer = <see cref="Name"/>.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Ob das Gerät Einstellungen hat.</summary>
        public bool HasSettings { get; set; }
    }

    /// <summary>Das Ergebnis von <see cref="IAgentObjectCatalog.ApplySettingsAsync"/>.</summary>
    public class ApplySettingsResult
    {
        /// <summary>Ob die Einstellungen abgelegt wurden.</summary>
        public bool Success { get; set; }

        /// <summary>Fehler, die keinem Feld gehören (unbekanntes Gerät, Ablage gescheitert).</summary>
        public string? Message { get; set; }

        /// <summary>Fehler je Feld.</summary>
        public SettingError[] Errors { get; set; } = Array.Empty<SettingError>();
    }

    /// <summary>Ein Fehler an einem Feld.</summary>
    /// <remarks>
    /// Die Prüfung läuft auf dem <b>Agenten</b>, angezeigt wird im Web. Die Sprache des Agenten-Rechners ist
    /// nicht die des Benutzers — darum nennt <see cref="Message"/> das Feld nur beim <b>Namen</b> (nie bei der
    /// Beschriftung, die eine JSON-Übersetzung sein kann), und die Maske formuliert anhand von
    /// <see cref="Kind"/> selbst. <see cref="Message"/> bleibt der Rückfall für Agenten, die
    /// <see cref="Kind"/> noch nicht setzen.
    /// </remarks>
    public class SettingError
    {
        /// <summary>Das Feld; leer = das Ganze.</summary>
        public string? SettingName { get; set; }

        /// <summary>Was nicht stimmt, technisch und englisch; nennt das Feld beim Namen.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Die Art des Fehlers; <see cref="SettingErrorKind.Unspecified"/> = nur <see cref="Message"/> zählt.</summary>
        public SettingErrorKind Kind { get; set; }

        /// <summary>Bei <see cref="SettingErrorKind.Invalid"/>: warum der Wert nicht gelesen werden konnte.</summary>
        public string? Detail { get; set; }
    }

    /// <summary>Die Art eines <see cref="SettingError"/>.</summary>
    /// <remarks>Nur anhängen: der Wert reist als Zahl zwischen Agent und Web, die beide verschieden alt sein können.</remarks>
    public enum SettingErrorKind
    {
        /// <summary>Nicht eingeordnet (älterer Agent, oder ein Fehler, der das Ganze betrifft).</summary>
        Unspecified = 0,

        /// <summary>Ein Pflichtfeld hat keinen Wert.</summary>
        Required = 1,

        /// <summary>Der Wert lässt sich nicht in den Typ des Felds lesen.</summary>
        Invalid = 2,

        /// <summary>Der Schlüssel ist kein Feld dieses Geräts.</summary>
        Unknown = 3
    }
}
