using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options
{
    /// <summary>
    /// Die Zustimmungen, die bei der Anmeldung eingeholt werden - Nutzungsbedingungen, Datenschutz,
    /// Newsletter und was ein Betrieb sonst braucht.
    /// </summary>
    /// <remarks>
    /// Bewusst reine Konfiguration und kein Plugin-Vertrag: an einem Zustimmungspunkt gibt es nichts zu
    /// entscheiden, nur etwas anzuzeigen und festzuhalten. Ein Betrieb, der seine Nutzungsbedingungen
    /// einholen will, soll dafuer keine Klasse schreiben und kein Plugin anmelden muessen.
    /// <para>
    /// Ist nichts konfiguriert, bleibt die Erfassung wie bisher: ein einzelner Schalter fuer die
    /// Nutzungsbedingungen.
    /// </para>
    /// </remarks>
    [SettingName("Consent")]
    public class ConsentOptions
    {
        /// <summary>
        /// Die Zustimmungspunkte in der Reihenfolge, in der sie erscheinen. Leer = es gilt der einzelne
        /// eingebaute Schalter.
        /// </summary>
        public ConsentPointOptions[] Points { get; set; } = new ConsentPointOptions[0];
    }

    /// <summary>
    /// Ein einzelner Zustimmungspunkt.
    /// </summary>
    public class ConsentPointOptions
    {
        /// <summary>
        /// Der stabile Schluessel, unter dem der Nachweis abgelegt wird. Er darf sich nicht mehr aendern,
        /// sobald damit Zustimmungen erfasst wurden - sonst laesst sich spaeter nicht mehr sagen, wozu
        /// jemand zugestimmt hat.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Der Text neben dem Schalter. Klartext ODER Kultur-JSON
        /// (<c>{"de":"Ich akzeptiere die {0}.","fr":"J'accepte les {0}."}</c>).
        /// <para>
        /// Enthaelt der Text die Stelle <c>{0}</c>, wird dort der Verweis auf das Dokument eingesetzt;
        /// sonst steht der Verweis dahinter. Damit laesst sich der Satzbau je Sprache anders legen, ohne
        /// dass die Oberflaeche etwas davon wissen muss.
        /// </para>
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Der Slug des Hilfe-Themas, das den Text des Dokuments traegt (<c>/help/{slug}</c>). Leer = es
        /// wird kein Verweis angeboten.
        /// </summary>
        /// <remarks>
        /// Die Dokumente liegen als Hilfe-Themen, weil die schon alles koennen, was hier gebraucht wird:
        /// pro Sprache ein eigener Text, in Markdown verfasst, und ohne Anmeldung lesbar - Voraussetzung
        /// dafuer, dass sie beim anonymen Start ueberhaupt aufgehen. Ein Container mit
        /// <c>ShowInMenu = false</c> haelt sie aus der Produkthilfe heraus.
        /// </remarks>
        public string HelpSlug { get; set; }

        /// <summary>
        /// Die Beschriftung des Verweises. Klartext oder Kultur-JSON; leer = der Titel des Hilfe-Themas.
        /// </summary>
        public string LinkText { get; set; }

        /// <summary>
        /// Muss zugestimmt werden, um fortzufahren? Vorgabe ja. Ein freiwilliger Punkt (typisch der
        /// Newsletter) steht auf <c>false</c> - abgelehnt wird er trotzdem festgehalten.
        /// </summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// Der Stand des Dokuments, dem zugestimmt wird (etwa <c>2026-08</c> oder <c>v3</c>). Wird im
        /// Nachweis mitgefuehrt: aendern sich die Nutzungsbedingungen, ist an den alten Nachweisen ablesbar,
        /// welchem Wortlaut jemand zugestimmt hat. Leer ist erlaubt, aber eine vertane Gelegenheit.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Wen die Zustimmung betrifft: <c>User</c> (Vorgabe), <c>Tenant</c> oder <c>Both</c>. Siehe
        /// <see cref="Consent.ConsentScope"/> - davon haengt ab, wo der Punkt erscheint und ob er nach der
        /// ersten Antwort verschwindet.
        /// </summary>
        /// <remarks>
        /// Als Zeichenkette und nicht als Aufzaehlung, weil diese Einstellungen ueber
        /// <c>JsonHelper.FromJsonString</c> mit statischer Typisierung gelesen werden - eine unbekannte
        /// Schreibweise soll eine Protokollzeile geben und auf die Vorgabe zurueckfallen, nicht die ganze
        /// Einstellung unbrauchbar machen. Gross-/Kleinschreibung ist egal.
        /// </remarks>
        public string Scope { get; set; }

        /// <summary>
        /// Punkt vorlaeufig abschalten, ohne ihn aus der Konfiguration zu nehmen - die bereits erfassten
        /// Nachweise bleiben damit erklaerbar.
        /// </summary>
        public bool Disabled { get; set; }
    }
}
