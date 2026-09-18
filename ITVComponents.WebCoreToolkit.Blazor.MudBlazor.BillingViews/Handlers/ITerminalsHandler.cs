using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Die Nahtstelle der Geräte-Verwaltung (Achse C) zur Oberfläche.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CanView"/> und <see cref="CanManage"/> sind für die <b>Anzeige</b> gedacht — was
    /// jemand nicht darf, soll er nicht sehen. <b>Die Umsetzung prüft trotzdem in jeder Methode
    /// selbst</b>, samt Hauptschalter und Mandanten-Feature.
    /// </para>
    /// <para>
    /// Das ist kein Gürtel-und-Hosenträger: dieser Dienst ist im DI registriert und von jeder
    /// Komponente aus erreichbar. Ohne eigene Prüfung hinge die Sicherheit daran, dass niemand einen
    /// zweiten Aufrufer schreibt — und bei den Verkäufen fängt das die Dienstschicht ab, weil dort
    /// jeder Vorgang durch dieselbe Klammer läuft. Die Geräteverwaltung hat dieses Netz nicht.
    /// </para>
    /// </remarks>
    public interface ITerminalsHandler
    {
        /// <summary>Darf die Geräte sehen.</summary>
        bool CanView();

        /// <summary>Darf Geräte anlegen, ändern und abschalten.</summary>
        bool CanManage();

        /// <summary>Ist das Zahlungsmodul überhaupt eingeschaltet?</summary>
        bool IsEnabled();

        /// <summary>Die Geräte des aktiven Mandanten.</summary>
        Task<IReadOnlyList<TerminalDefinition>> GetTerminalsAsync(CancellationToken cancellationToken = default);

        /// <summary>Die Wege, die zur Auswahl stehen — der erste Schritt des Assistenten.</summary>
        IReadOnlyList<TerminalProviderInfo> GetProviders();

        /// <summary>Die Felder, die ein Weg im ersten Schritt braucht.</summary>
        IReadOnlyList<TerminalSettingDescriptor> DescribeSettings(string providerKey);

        /// <summary>
        /// Die Felder, die das Gerät selbst verlangt — der zweite Schritt. Leer, wenn es keinen gibt.
        /// </summary>
        Task<IReadOnlyList<TerminalSettingDescriptor>> DescribeDeviceSettingsAsync(string providerKey,
            string? configurationJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Füllt eine Auswahlliste, die erst zur Laufzeit feststeht.
        /// </summary>
        /// <param name="source">der Name der Quelle (<see cref="TerminalChoiceSources"/>)</param>
        /// <param name="dependsOnValue">
        /// der Wert des Feldes, von dem die Liste abhängt — bei den Objekten eines Dienstes dessen Name
        /// </param>
        /// <param name="cancellationToken">bricht die Abfrage ab</param>
        /// <returns>
        /// die Einträge, oder eine leere Liste. <b>Leer heisst „unbekannte Quelle" oder „nichts da"</b>;
        /// die Maske zeichnet dann ein Textfeld statt einer Auswahl — unschön, aber bedienbar.
        /// </returns>
        Task<IReadOnlyList<TerminalSettingChoice>> GetChoicesAsync(string source, string? dependsOnValue,
            CancellationToken cancellationToken = default);

        /// <summary>Legt ein Gerät an oder ändert es.</summary>
        Task<int> SaveAsync(TerminalDefinition definition, CancellationToken cancellationToken = default);

        /// <summary>Schaltet ein Gerät ein oder aus.</summary>
        Task SetEnabledAsync(int terminalId, bool enabled, CancellationToken cancellationToken = default);

        /// <summary>Fragt, ob ein Gerät gerade erreichbar ist.</summary>
        Task<TerminalStatus> GetStatusAsync(int terminalId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Füllt eine Auswahlliste, die das Toolkit nicht selbst kennt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mehrere davon dürfen nebeneinander registriert sein; jeder sagt über <see cref="Handles"/>, für
    /// welche Quelle er zuständig ist. Das Toolkit bringt den für die Client-Anwendungen mit; den für
    /// die Objekte eines Dienstes stellt die Anwendung, weil nur sie weiss, welche Plugins wo laufen.
    /// </para>
    /// <para>
    /// <b>Ein fehlender Anbieter ist kein Fehler.</b> Die Maske zeichnet dann ein Textfeld — wer den
    /// Namen kennt, kann ihn eintippen. Ein Feld ganz wegzulassen wäre schlimmer: dann fehlte etwas,
    /// ohne dass es auffällt.
    /// </para>
    /// </remarks>
    public interface ITerminalChoiceProvider
    {
        /// <summary>Ob dieser Anbieter die genannte Quelle füllen kann.</summary>
        bool Handles(string source);

        /// <summary>Die Einträge.</summary>
        Task<IReadOnlyList<TerminalSettingChoice>> GetChoicesAsync(string source, string? dependsOnValue,
            CancellationToken cancellationToken = default);
    }
}
