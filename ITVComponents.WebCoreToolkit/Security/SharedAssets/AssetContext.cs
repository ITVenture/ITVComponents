using System;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Die Auskunft an eine Seite: <b>laeuft das hier gerade in einer Freigabe, und wenn ja, in was fuer
    /// einer?</b> Unveraenderlich und absichtlich schmal.
    /// <para>
    /// Abgrenzung zu <see cref="ISharedAssetContext.CurrentAsset"/>: das ist die Arbeitsfassung fuer den
    /// Riegel und das Protokoll und traegt Rechte, Features und die Argumentwerte der Freigabe. Die
    /// gehoeren nicht in eine Auskunft - eine Seite soll wissen, wo sie steht, und nicht, womit sie
    /// hereingekommen ist.
    /// </para>
    /// <para>
    /// <b>Was hier bewusst NICHT steht, ist der Riegel-Zustand</b> (<c>Confirmed</c>,
    /// <c>MustHoldBack</c>): der aendert sich waehrend des Vorgangs, ein Schnappschuss davon wuerde
    /// luegen. Diese Fragen gehen weiter an <see cref="ISharedAssetContext"/>.
    /// </para>
    /// </summary>
    public sealed record AssetContext
    {
        /// <summary>
        /// Der Schluessel der Freigabe - bei einem Ad-hoc-Ticket dessen Kennung (Nonce).
        /// </summary>
        public string AssetKey { get; init; }

        /// <summary>
        /// Der rohe Pfad-Abschnitt der laufenden Anfrage (mit Marker, ohne Schraegstriche).
        /// </summary>
        public string Segment { get; init; }

        /// <summary>
        /// Ob der Kontext an einer gespeicherten Freigabe oder an einem Ad-hoc-Ticket haengt.
        /// </summary>
        public AssetSegmentKind Kind { get; init; }

        /// <summary>
        /// Kurzform fuer "das ist ein Ad-hoc-Ticket" - eine Freigabe, die nirgends steht.
        /// </summary>
        public bool IsAdHoc => Kind == AssetSegmentKind.Ticket;

        /// <summary>
        /// Der Pfadbereich, auf dem geteilt wurde (<c>SharedAsset.RootPath</c> bzw. der Pfad im Ticket).
        /// </summary>
        public string RootPath { get; init; }

        /// <summary>
        /// Der Mandant, in dem diese Anfrage durch die Freigabe arbeitet.
        /// </summary>
        public string TenantName { get; init; }

        /// <summary>
        /// Der Titel der Freigabe, oder null (Tickets haben keinen).
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// Die Vorlage, aus der die Rechte stammen. Das Einzige, was gespeicherte Freigaben und Tickets
        /// gemeinsam haben.
        /// </summary>
        public string TemplateSystemKey { get; init; }

        /// <summary>
        /// An wen die Freigabe gerichtet ist. <b>Eine Behauptung, kein Nachweis</b> - als Anzeige
        /// brauchbar, als Entscheidungsgrundlage nicht.
        /// </summary>
        public string RecipientLabel { get; init; }

        /// <summary>
        /// Ob die Freigabe ohne Anmeldung benutzt werden darf.
        /// </summary>
        public bool AllowsAnonymousAccess { get; init; }

        /// <summary>
        /// Ob der benutzte Link sein Zugangsgeheimnis selbst traegt - ein anonymer Link oder ein Ticket.
        /// </summary>
        public bool ViaAnonymousLink { get; init; }

        /// <summary>
        /// Ob hinter diesem Zugriff <b>wirklich niemand</b> steht.
        /// <para>
        /// Das ist die eigentliche Unterscheidung: dass ein Link anonym benutzt werden DARF, heisst nicht,
        /// dass es egal ist, wer ihn benutzt. Ist der Besucher angemeldet, steht hier false und sein Name
        /// steht ueberall, wo sonst <c>#ANONYMOUS#</c> stuende - an den Rechten der Freigabe aendert das
        /// nichts, die gelten fuer jeden gleich.
        /// </para>
        /// </summary>
        public bool VisitorIsAnonymous { get; init; }

        /// <summary>
        /// Ab wann die Freigabe gilt, oder null.
        /// </summary>
        public DateTime? NotBefore { get; init; }

        /// <summary>
        /// Bis wann die Freigabe gilt, oder null. Bei einem Ticket immer gesetzt - was nirgends steht,
        /// muss von selbst enden.
        /// </summary>
        public DateTime? NotAfter { get; init; }
    }
}
