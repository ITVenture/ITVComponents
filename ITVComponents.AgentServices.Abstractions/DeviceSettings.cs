namespace ITVComponents.AgentServices.Abstractions
{
    /// <summary>
    /// Kennzeichnet ein Gerät am Agenten, das Einstellungen vom Typ <typeparamref name="TSettings"/> hat.
    /// </summary>
    /// <typeparam name="TSettings">
    /// der Einstellungstyp. Nur seine Properties mit <see cref="DeviceSettingAttribute"/> sind Felder —
    /// was dort kein Feld hat, existiert für die Web-Anwendung nicht.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Ein reiner Marker: der Katalog liest den Einstellungstyp per Reflexion am Plugin-<b>Typ</b> ab, ohne
    /// das Gerät zu bauen — ein Konstruktor, der die Hardware öffnet, darf nicht deshalb laufen, weil
    /// jemand in der Web-Anwendung eine Maske aufklappt.
    /// </para>
    /// <para>
    /// Die Werte bekommt das Gerät im Konstruktor: ein Parameter <c>$DeviceSettings</c> in der
    /// Konstruktionszeile wird je Gerät aus der lokalen Ablage aufgelöst
    /// (<see cref="AgentCatalog.DeviceSettingsParameterName"/>).
    /// </para>
    /// </remarks>
    public interface IHasDeviceSettings<TSettings> where TSettings : class, new()
    {
    }

    /// <summary>
    /// Macht eine Property eines Einstellungstyps zu einem Feld in der Maske.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DeviceSettingAttribute : Attribute
    {
        /// <summary>Initialisiert das Attribut.</summary>
        /// <param name="label">die Beschriftung in der Maske</param>
        public DeviceSettingAttribute(string label)
        {
            Label = label;
        }

        /// <summary>Die Beschriftung in der Maske.</summary>
        public string Label { get; }

        /// <summary>Ob ein Wert gesetzt sein muss.</summary>
        public bool Required { get; set; }

        /// <summary>Ein erklärender Satz unter dem Feld.</summary>
        public string? HelpText { get; set; }

        /// <summary>
        /// Der Name eines <see cref="IDeviceChoiceProvider"/> am Agenten, der die Auswahl liefert — für
        /// Werte, die nur der Rechner vor Ort kennt (installierte Drucker, Schnittstellen).
        /// </summary>
        public string? ChoiceSource { get; set; }

        /// <summary>Der Name eines anderen Felds, von dem die Auswahl abhängt.</summary>
        public string? DependsOn { get; set; }

        /// <summary>
        /// Die Art der Eingabe. Ohne Angabe aus dem Property-Typ abgeleitet; mit <see cref="ChoiceSource"/>
        /// immer <see cref="SettingKind.Choice"/>.
        /// </summary>
        public SettingKind Kind { get; set; } = (SettingKind)(-1);

        /// <summary>Die Reihenfolge in der Maske; gleich = Reihenfolge der Deklaration.</summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Liefert Auswahlwerte, die nur der Rechner vor Ort kennt.
    /// </summary>
    /// <remarks>
    /// Wird am Agenten als Plugin unter dem Namen konfiguriert, den
    /// <see cref="DeviceSettingAttribute.ChoiceSource"/> nennt. <b>Nie das Gerät selbst</b> — sonst würde
    /// das Aufklappen einer Liste das Gerät bauen.
    /// </remarks>
    public interface IDeviceChoiceProvider
    {
        /// <summary>Die Auswahl für ein Feld.</summary>
        /// <param name="settingName">das Feld, für das gefragt wird</param>
        /// <param name="draft">die Werte, die in der Maske gerade stehen (für abhängige Felder)</param>
        Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(string settingName,
            IReadOnlyDictionary<string, string?> draft);
    }
}
