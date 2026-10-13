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
    /// <param name="ClientAppName">
    /// die Anwendung, fuer die gekoppelt wird - nur, wenn das Geraet sie beim Anlegen selbst festgelegt hat
    /// (mit seinem <c>ClientKey</c>); sonst null, und die Anwendung wird aus <see cref="Candidates"/> gewaehlt
    /// </param>
    /// <param name="DeviceLabel">wie sich das Geraet nennt</param>
    /// <param name="PermissionSets">die Rechtebuendel, die die festgelegte Anwendung zugestanden bekommt</param>
    /// <param name="ClientAppId">die festgelegte Anwendung, sonst null</param>
    /// <param name="ClientAppTemplateId">was das Geraet ist (das Template seines Typ-Schluessels)</param>
    /// <param name="TemplateName">der Name dieses Templates, zur Anzeige</param>
    /// <param name="Candidates">
    /// die Anwendungen dieses Templates im Mandanten des Bestaetigers - die Auswahl, wenn keine Anwendung
    /// festgelegt ist
    /// </param>
    /// <param name="TemplatePermissionSets">
    /// die Rechtebuendel des Templates - fuer das Anlegen einer neuen Anwendung aus der Bestaetigung heraus
    /// </param>
    public sealed record PairingPreview(
        bool Found,
        string ClientAppName,
        string DeviceLabel,
        IReadOnlyList<string> PermissionSets,
        int? ClientAppId = null,
        int? ClientAppTemplateId = null,
        string TemplateName = null,
        IReadOnlyList<PairingCandidate> Candidates = null,
        IReadOnlyList<PairingPermissionSet> TemplatePermissionSets = null);

    /// <summary>
    /// Eine Anwendung, an die ein Geraet gekoppelt werden kann.
    /// </summary>
    /// <param name="ClientAppId">die Anwendung</param>
    /// <param name="ClientAppName">ihr Name</param>
    /// <param name="PermissionSets">die Rechtebuendel, die sie zugestanden bekommt</param>
    /// <param name="ActiveInstance">
    /// das Geraet, das schon an ihr gekoppelt ist (sein Name), sonst null. <b>Eine Anwendung hat hoechstens
    /// eine aktive Instanz</b>; ist hier etwas eingetragen, laesst sich nur dasselbe Geraet erneut koppeln
    /// (es ersetzt dann seinen Zugang), jedes andere wird abgewiesen.
    /// </param>
    /// <param name="Available">ob dieses Geraet an diese Anwendung gekoppelt werden kann</param>
    public sealed record PairingCandidate(
        int ClientAppId,
        string ClientAppName,
        IReadOnlyList<string> PermissionSets,
        string ActiveInstance,
        bool Available);

    /// <summary>
    /// Ein Rechtebuendel eines Templates.
    /// </summary>
    /// <param name="AppPermissionSetId">das Buendel</param>
    /// <param name="Name">sein Name</param>
    public sealed record PairingPermissionSet(int AppPermissionSetId, string Name);

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
    /// <param name="ClientKey">
    /// die Kennung der Anwendung, an die gekoppelt wurde - zusammen mit <see cref="ApiKey"/> und nur dort.
    /// Das Geraet meldet sich damit am Dienst-Verteiler an. Steht auch vorne im Schluessel; hier ausgewiesen,
    /// damit niemand den Schluessel zerlegen muss.
    /// </param>
    public sealed record PairingResult(
        DevicePairingState State,
        string ApiKey,
        int PollIntervalSeconds,
        string ClientKey = null);
}
