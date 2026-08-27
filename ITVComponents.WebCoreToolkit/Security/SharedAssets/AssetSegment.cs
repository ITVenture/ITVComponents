namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Was in einem Asset-Abschnitt der URL stehen kann.
    /// </summary>
    public enum AssetSegmentKind
    {
        /// <summary>Kein Asset-Abschnitt.</summary>
        None = 0,

        /// <summary>
        /// Der Verweis auf eine gespeicherte Freigabe: <c>~{key}[.{token}]</c>.
        /// </summary>
        StoredAsset = 1,

        /// <summary>
        /// Ein Ad-hoc-Ticket: <c>~!{tenant}.{payload}</c>. Es steht nirgends - alles, was es ausmacht,
        /// reist verschluesselt mit.
        /// </summary>
        Ticket = 2
    }

    /// <summary>
    /// Der zerlegte Asset-Abschnitt einer Anfrage.
    /// </summary>
    public sealed class AssetSegment
    {
        /// <summary>Der rohe Abschnitt, wie er in der URL steht (mit Marker).</summary>
        public string Raw { get; set; }

        public AssetSegmentKind Kind { get; set; }

        /// <summary>Der Schluessel der gespeicherten Freigabe, oder null.</summary>
        public string AssetKey { get; set; }

        /// <summary>Das Zugangs-Token einer gespeicherten Freigabe, oder null.</summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Der Mandant eines Tickets - <b>im Klartext</b>, denn ohne ihn liesse sich der Schluessel zum
        /// Entschluesseln gar nicht bestimmen. Kein Verlust: bei Hosts mit Mandant im Pfad steht er
        /// ohnehin in derselben URL.
        /// </summary>
        public string TenantName { get; set; }

        /// <summary>Die verschluesselte Nutzlast eines Tickets, oder null.</summary>
        public string Payload { get; set; }
    }
}
