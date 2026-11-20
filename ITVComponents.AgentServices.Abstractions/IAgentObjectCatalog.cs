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

        /// <summary>
        /// Die Vorlagen der installierten Module: Typen, von denen sich unter eigenem Namen Geräte (oder andere
        /// Plugins) anlegen lassen.
        /// </summary>
        /// <param name="contractTypeName">der volle Name des Vertrags; leer = alle Vorlagen</param>
        /// <returns>die Vorlagen; leer, wenn der Agent keine Modul-Plugins kennt oder keine passt</returns>
        Task<AgentTemplateInfo[]> GetTemplatesAsync(string contractTypeName);

        /// <summary>
        /// Legt von einer Vorlage ein Gerät an: prüft die Einstellungen, legt sie ab und lässt die Instanz vom
        /// Update-Dienst eintragen.
        /// </summary>
        /// <param name="templateKey">die Vorlage (<see cref="AgentTemplateInfo.Key"/>)</param>
        /// <param name="name">der Name des neuen Geräts</param>
        /// <param name="settingsJson">die Einstellungen als JSON-Objekt; leer bei einer Vorlage ohne Einstellungen</param>
        /// <returns>das Ergebnis; bei einem Fehler wurde nichts angelegt</returns>
        Task<AgentObjectChangeResult> CreateObjectAsync(string templateKey, string name, string? settingsJson);

        /// <summary>
        /// Entfernt ein Gerät, das von einer Vorlage angelegt wurde, samt seinen Einstellungen.
        /// </summary>
        /// <param name="name">der Name des Geräts</param>
        /// <returns>das Ergebnis</returns>
        Task<AgentObjectChangeResult> RemoveObjectAsync(string name);

        /// <summary>
        /// Die Auswahl für ein Feld einer Vorlage, bevor es das Gerät gibt — für die Maske, in der es angelegt wird.
        /// </summary>
        /// <param name="templateKey">die Vorlage (<see cref="AgentTemplateInfo.Key"/>)</param>
        /// <param name="settingName">das Feld</param>
        /// <param name="draftJson">die Werte, die in der Maske gerade stehen, als JSON-Objekt</param>
        Task<SettingChoice[]> GetTemplateChoicesAsync(string templateKey, string settingName, string? draftJson);

        /// <summary>
        /// Alle Geräte (und anderen Plugins) aus Modulen, die der Agent kennt — in jedem Lademodus, auch solche, deren
        /// Modul fehlt (Issue ModulePlugins-Instances, C.4).
        /// </summary>
        /// <returns>die Instanzen; leer, wenn der Agent keine Modul-Plugins kennt</returns>
        Task<AgentModuleObjectInfo[]> GetModuleObjectsAsync();
    }

    /// <summary>Eine Instanz aus einem Modul, wie der Agent sie kennt.</summary>
    public class AgentModuleObjectInfo
    {
        /// <summary>Der Name der Instanz.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Das Modul, aus dem sie kommt.</summary>
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>Der volle Name des Typs.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Die Vorlage, aus der sie angelegt wurde (<c>&lt;Modul&gt;/&lt;Typ&gt;</c>); leer bei Infrastruktur.</summary>
        public string? TemplateKey { get; set; }

        /// <summary>Wie sie in einer Auswahl heissen soll; leer = <see cref="Name"/>.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Wann sie entsteht: <c>Singleton</c>, <c>ScopeStartup</c> oder <c>Scope</c>.</summary>
        public string LoadType { get; set; } = string.Empty;

        /// <summary>Ob sie sich entfernen lässt — nur, was aus einer Vorlage angelegt wurde.</summary>
        public bool Removable { get; set; }

        /// <summary>Ob der Agent sie lädt.</summary>
        public bool Usable { get; set; }

        /// <summary>Warum nicht, technisch und englisch (z.B. das Modul fehlt).</summary>
        public string? Problem { get; set; }

        /// <summary>Ob das Modul fehlt — der häufigste Grund, eigens benannt, damit die Maske ihn übersetzen kann.</summary>
        public bool ModuleMissing { get; set; }
    }

    /// <summary>Eine Vorlage eines installierten Moduls.</summary>
    public class AgentTemplateInfo
    {
        /// <summary>Der Schlüssel, mit dem sie gewählt wird: <c>&lt;Modul&gt;/&lt;Typ&gt;</c>.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Das Modul, das sie mitbringt.</summary>
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>Der volle Name des Typs.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Wie sie in einer Auswahl heissen soll; leer = <see cref="TypeName"/>.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Wann ein angelegtes Gerät entsteht: <c>Singleton</c>, <c>ScopeStartup</c> oder <c>Scope</c>.</summary>
        public string LoadType { get; set; } = string.Empty;

        /// <summary>Ob die Vorlage Einstellungen hat.</summary>
        public bool HasSettings { get; set; }

        /// <summary>Die Felder der Einstellungen; leer ohne Einstellungen.</summary>
        public SettingDescriptor[] Settings { get; set; } = Array.Empty<SettingDescriptor>();
    }

    /// <summary>Das Ergebnis von <see cref="IAgentObjectCatalog.CreateObjectAsync"/> und <see cref="IAgentObjectCatalog.RemoveObjectAsync"/>.</summary>
    public class AgentObjectChangeResult
    {
        /// <summary>Ob das Gerät angelegt bzw. entfernt wurde.</summary>
        public bool Success { get; set; }

        /// <summary>Was nicht stimmt, technisch und englisch; bei Erfolg ein Hinweis, falls etwas nur teilweise gelang.</summary>
        public string? Message { get; set; }

        /// <summary>Fehler je Feld der Einstellungen.</summary>
        public SettingError[] Errors { get; set; } = Array.Empty<SettingError>();

        /// <summary>Ob die Änderung erst nach einem Neustart des Agenten wirkt (Lademodus <c>Singleton</c>).</summary>
        public bool EffectiveAfterRestart { get; set; }
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
