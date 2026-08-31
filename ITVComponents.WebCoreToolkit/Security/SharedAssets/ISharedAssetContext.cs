using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Host-neutraler Zugriff auf das geteilte Asset des aktuellen Kontexts. Ersetzt das fruehere "in
    /// <c>Request.Query</c> greifen (und notfalls in den <c>Referer</c>)", das jeder Konsument fuer sich
    /// gemacht hat: der Schluessel reist als Pfad-Abschnitt, und ein Blazor-Circuit - der weder eine
    /// Query noch einen brauchbaren Referer je Navigation hat - kann aus seiner Basis-URI antworten.
    /// <para>
    /// Ausserdem fuehrt er die <b>Bestaetigung der Argumente</b>: die Antwort auf "darf dieses Objekt an
    /// ihn raus". Die Rechte-Frage ist damit nicht gemeint - die ist am Eintritt schon beantwortet.
    /// </para>
    /// </summary>
    public interface ISharedAssetContext
    {
        /// <summary>
        /// Gibt an, ob der aktuelle Kontext in einem geteilten Asset laeuft.
        /// </summary>
        bool HasAsset { get; }

        /// <summary>
        /// Der Schluessel des Assets, in dem der aktuelle Kontext laeuft, oder null.
        /// </summary>
        string AssetKey { get; }

        /// <summary>
        /// Das Zugangs-Token des aktuellen Kontexts, oder null. Nur bei Links fuer anonyme Empfaenger.
        /// </summary>
        string AccessToken { get; }

        /// <summary>
        /// Der rohe Pfad-Abschnitt (mit Marker, ohne Schraegstriche) des aktuellen Assets, oder null.
        /// Das ist, was der Linkbau voranstellt; siehe <see cref="SharedAssetPath.BuildPrefix"/>.
        /// </summary>
        string Segment { get; }

        /// <summary>
        /// Ob der Kontext an einer gespeicherten Freigabe oder an einem Ad-hoc-Ticket haengt.
        /// </summary>
        AssetSegmentKind SegmentKind { get; }

        /// <summary>
        /// Der Mandant eines Ad-hoc-Tickets, oder null. Steht im Klartext im Abschnitt - ohne ihn liesse
        /// sich der Schluessel zum Entschluesseln nicht bestimmen.
        /// </summary>
        string TicketTenant { get; }

        /// <summary>
        /// Die verschluesselte Nutzlast eines Ad-hoc-Tickets, oder null.
        /// </summary>
        string TicketPayload { get; }

        /// <summary>
        /// Die Angaben zur Freigabe des aktuellen Kontexts, oder null. Fuer den Riegel und das Protokoll -
        /// nicht als Auskunft an eine Seite gedacht.
        /// </summary>
        AssetInfo CurrentAsset { get; }

        /// <summary>
        /// Die Auskunft an eine Seite: in welcher Freigabe laeuft das hier gerade? <b>Null, wenn keine
        /// laeuft</b> - und ebenso, wenn der Abschnitt zu keiner (mehr) gueltigen Freigabe gehoert; wo
        /// keine Rechte verliehen werden, laeuft auch nichts in einer Freigabe.
        /// <para>
        /// <b>Kostet einmal je Scope einen Zugriff auf die Ablage.</b> Wer nur wissen will, OB eine
        /// Freigabe laeuft, fragt <see cref="HasAsset"/> - das beantwortet der Pfad allein.
        /// </para>
        /// </summary>
        AssetContext AssetContext { get; }

        /// <summary>
        /// Wie streng die Vorlage die Bestaetigung ihrer Argumente verlangt. <c>None</c>, wenn kein Asset
        /// laeuft oder die Vorlage keine Argumente fuehrt.
        /// </summary>
        AssetArgumentEnforcement Enforcement { get; }

        /// <summary>
        /// Gibt an, ob in diesem Kontext <b>alle Pflichtargumente</b> der Freigabe bestaetigt wurden.
        /// </summary>
        bool Confirmed { get; }

        /// <summary>
        /// Gibt an, ob eine Bestaetigung fehlgeschlagen ist. Einmal true, bleibt es true - eine spaetere
        /// erfolgreiche Bestaetigung darf einen bereits abgelehnten Zugriff nicht weisswaschen.
        /// </summary>
        bool Denied { get; }

        /// <summary>
        /// Die Frage, die der Riegel am Ausgang stellt: muss die Antwort zurueckgehalten werden?
        /// <para>
        /// True, wenn die Vorlage eine Bestaetigung verlangt und keine (vollstaendige) vorliegt, oder wenn
        /// eine Bestaetigung fehlgeschlagen ist. <b>Der vergessene Aufruf faellt damit als leere Seite
        /// auf und nicht als stilles Loch.</b>
        /// </para>
        /// </summary>
        bool MustHoldBack { get; }

        /// <summary>
        /// Bestaetigt, dass der aktuelle Vorgang genau das Objekt betrifft, auf das die Freigabe zeigt.
        /// <para>
        /// Der richtige Zeitpunkt ist der, an dem der Wert <b>sicher</b> bekannt ist: in MVC nach dem
        /// Model-Binding, in Blazor sobald der Datensatz geladen ist, in einer Datei-Behandlung nach dem
        /// Aufloesen des Tokens und <b>vor</b> dem Streamen.
        /// </para>
        /// <para>
        /// Laeuft kein Asset, ist die Antwort true: es gibt nichts zu bestaetigen, und der Aufrufer soll
        /// deshalb nicht anders arbeiten muessen.
        /// </para>
        /// </summary>
        /// <param name="name">der Name des Arguments</param>
        /// <param name="value">der Wert, den der aktuelle Vorgang betrifft</param>
        /// <returns>true, wenn der Wert zur Freigabe gehoert</returns>
        bool Require(string name, object value);

        /// <summary>
        /// Beginnt einen neuen Vorgang: unter <see cref="AssetArgumentEnforcement.Strict"/> verfaellt damit
        /// eine frueher erteilte Bestaetigung, unter <see cref="AssetArgumentEnforcement.Confirmed"/>
        /// passiert nichts.
        /// <para>
        /// <b>Darin besteht der Unterschied der beiden Grade</b>, und er zaehlt genau dort, wo ein Kontext
        /// laenger lebt als ein Vorgang: eine MVC-Anfrage bekommt ohnehin ihren eigenen Scope, ein
        /// Blazor-Circuit aber nicht - dort wuerde eine einmalige Bestaetigung sonst alles Weitere
        /// mitdecken.
        /// </para>
        /// <para>
        /// Eine <b>Ablehnung</b> nimmt das nicht zurueck: was einmal auf ein fremdes Objekt gezeigt hat,
        /// bleibt abgelehnt.
        /// </para>
        /// </summary>
        void ResetConfirmation();

        /// <summary>
        /// Bestaetigt mehrere Argumente auf einmal. Schlaegt eines fehl, ist das Ergebnis false - und der
        /// Kontext bleibt abgelehnt.
        /// </summary>
        /// <param name="values">die Werte des aktuellen Vorgangs</param>
        /// <returns>true, wenn alle Werte zur Freigabe gehoeren</returns>
        bool Require(IDictionary<string, object> values);
    }
}
