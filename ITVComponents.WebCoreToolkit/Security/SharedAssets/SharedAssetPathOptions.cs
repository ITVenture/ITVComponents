namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Options for the shared-asset path context.
    /// </summary>
    public class SharedAssetPathOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the deprecated query form
        /// (<c>?SharedAssetKey=…&amp;__AccessToken=…</c>) is still accepted as a source. Defaults to true so
        /// links that were already sent out keep working. A host whose old links have expired can switch it
        /// off; new links are always created in the path form.
        /// </summary>
        public bool AcceptQuerySharedAssetKey { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the <c>Referer</c> of a request may serve as a last
        /// resort for the query form. Only relevant while <see cref="AcceptQuerySharedAssetKey"/> is on -
        /// the path form needs no such fallback, because sub-resources inherit the prefix.
        /// </summary>
        public bool AcceptRefererFallback { get; set; } = true;
    }
}
