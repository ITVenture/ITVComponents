using System;
using System.Collections.Generic;
using System.Globalization;

namespace ITVComponents.WebCoreToolkit.Localization
{
    /// <summary>
    /// Die Ortseinstellungen eines Mandanten (Zeitzone, Kultur), aufgelöst bis zu einem verwendbaren Wert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Für alles, was ein Mandant <b>ausgibt</b> — Belege, Abschlüsse, Mails, Rechnungen. Die Sprache des Lesers
    /// (<see cref="CultureInfo.CurrentUICulture"/>) ist dafür die falsche Grundlage: ein Beleg entsteht im Webhook,
    /// an einer anonymen Station, ohne angemeldeten Benutzer, und soll in der Sprache des Ladens erscheinen.
    /// </para>
    /// <para>
    /// Rückfall je Wert, unabhängig voneinander: eigener Wert → übergeordnete Mandanten (siehe
    /// <see cref="ITVComponents.WebCoreToolkit.Security.ISecurityRepository.GetTenantLocale"/>) → Vorgabe des Hosts
    /// (Zeitzone: die des Servers; Kultur: die <c>DefaultRequestCulture</c> der Request-Localization, sonst die
    /// erste der unterstützten).
    /// </para>
    /// <para>
    /// Die Kultur wird gespeichert, wie sie gewählt wurde — spezifisch (<c>de-CH</c>) oder nur als Sprache
    /// (<c>de</c>). Zur Sprache gehört beim Mandanten auch sein Format: ein Laden in Zürich und einer in München
    /// auf demselben Host unterscheiden sich darin, und das kann eine Zuordnung des Hosts nicht leisten.
    /// </para>
    /// <para>
    /// Braucht keinen angemeldeten Benutzer — nur den Namen des Mandanten.
    /// </para>
    /// </remarks>
    public interface ITenantLocaleResolver
    {
        /// <summary>Die Zeitzone des Mandanten.</summary>
        /// <param name="permissionScopeName">der Mandant</param>
        TimeZoneInfo GetTimeZone(string permissionScopeName);

        /// <summary>
        /// Die Kultur des Mandanten für Texte und Ressourcen, wie gespeichert (<c>de-CH</c>, oder <c>de</c>).
        /// </summary>
        /// <remarks>
        /// Gilt, sobald der Host ihre <b>Sprache</b> anbietet — auch in einer Ausprägung, die er nicht eigens
        /// führt (<c>de-AT</c> bei <c>de-CH</c>/<c>de-DE</c>): die Texte findet der Localizer über seinen
        /// Rückfall (<c>de-AT</c> → <c>de</c> → neutral). Eine Sprache, die der Host gar nicht anbietet, ergibt
        /// die Vorgabe des Hosts.
        /// </remarks>
        /// <param name="permissionScopeName">der Mandant</param>
        CultureInfo GetUICulture(string permissionScopeName);

        /// <summary>
        /// Die Kultur für Zahlen und Datumsangaben: <see cref="GetUICulture"/>, durch die Zuordnung des Hosts
        /// (<see cref="ITVComponents.WebCoreToolkit.Options.CultureOptions.MapCulture(CultureInfo)"/>) geschickt.
        /// Eine nur als Sprache gespeicherte Kultur bekommt so ihr Format (<c>de</c> → <c>de-CH</c>); eine
        /// spezifische ohne Zuordnung bleibt, wie sie ist.
        /// </summary>
        /// <param name="permissionScopeName">der Mandant</param>
        CultureInfo GetCulture(string permissionScopeName);

        /// <summary>
        /// Die Kulturen, die ein Mandant wählen kann: die <c>SupportedUICultures</c> des Hosts, wie konfiguriert,
        /// ohne Doppelte.
        /// </summary>
        IReadOnlyList<CultureInfo> SupportedCultures { get; }

        /// <summary>
        /// Der Eintrag aus <see cref="SupportedCultures"/>, der zu einer Kultur passt — zum Vorbelegen einer
        /// Auswahl, z.B. mit der aktuellen UI-Kultur.
        /// </summary>
        /// <returns>
        /// Der gleichnamige Eintrag, sonst der erste mit derselben Sprache, sonst <c>null</c>. Führt der Host
        /// keine Liste, die Kultur selbst (<c>null</c> für die invariante).
        /// </returns>
        CultureInfo FindSupportedCulture(CultureInfo culture);
    }
}
