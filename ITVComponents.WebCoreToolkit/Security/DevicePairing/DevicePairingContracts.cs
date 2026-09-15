using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Security.DevicePairing
{
    /// <summary>
    /// Was das Geraet nach <c>StartAsync</c> bekommt.
    /// </summary>
    /// <param name="DeviceCode">
    /// <b>Das Geheimnis des Vorgangs.</b> Es verlaesst den Agenten nie; in der Ablage steht nur sein Hash.
    /// Nur wer ihn hat, kann das Ergebnis abholen.
    /// </param>
    /// <param name="UserCode">
    /// Der Code zum Abtippen. <b>Kein Geheimnis</b> - er darf auf einem Bildschirm im Laden stehen. Allein
    /// bringt er niemanden weiter, weil zum Abholen der Geraetecode noetig ist.
    /// </param>
    /// <param name="ExpiresUtc">wann der Vorgang verfaellt</param>
    /// <param name="PollIntervalSeconds">wie oft das Geraet fruehestens nachfragen darf</param>
    public sealed record PairingRequest(
        string DeviceCode,
        string UserCode,
        System.DateTime ExpiresUtc,
        int PollIntervalSeconds);

    /// <summary>
    /// Was die Bestaetigungsmaske vor dem Klick anzeigen soll.
    /// </summary>
    /// <remarks>
    /// <b>Der Grund, warum es diese Abfrage gibt:</b> ein Knopf, hinter dem niemand weiss, was er
    /// freigibt, ist keine Zustimmung. Die Maske zeigt Anwendung, Geraet und die Rechtebuendel, die
    /// gleich erteilt werden.
    /// </remarks>
    /// <param name="Found">ob der Code ueberhaupt zu einem offenen Vorgang gehoert</param>
    /// <param name="ClientAppName">die Anwendung, fuer die gekoppelt wird</param>
    /// <param name="DeviceLabel">wie sich das Geraet nennt</param>
    /// <param name="PermissionSets">die Rechtebuendel, die diese Anwendung im Mandanten zugestanden bekommt</param>
    public sealed record PairingPreview(
        bool Found,
        string ClientAppName,
        string DeviceLabel,
        IReadOnlyList<string> PermissionSets);

    /// <summary>
    /// Das Ergebnis der Bestaetigung.
    /// </summary>
    /// <param name="Success">ob bestaetigt wurde</param>
    /// <param name="Reason">im Misserfolgsfall der Grund, sonst null</param>
    public sealed record PairingConfirmation(bool Success, string Reason);

    /// <summary>
    /// Was das Geraet beim Nachfragen bekommt.
    /// </summary>
    /// <param name="State">der Zustand des Vorgangs</param>
    /// <param name="ApiKey">
    /// der fertige Schluessel <c>&lt;ClientKey&gt;.&lt;Label&gt;.&lt;Geheimnis&gt;</c> - <b>genau einmal</b>,
    /// naemlich bei dem Nachfragen, das den Vorgang von <c>Confirmed</c> auf <c>Delivered</c> setzt. Jedes
    /// weitere Nachfragen bekommt hier null.
    /// </param>
    /// <param name="PollIntervalSeconds">wie lange das Geraet bis zum naechsten Versuch warten soll</param>
    public sealed record PairingResult(
        DevicePairingState State,
        string ApiKey,
        int PollIntervalSeconds);
}
