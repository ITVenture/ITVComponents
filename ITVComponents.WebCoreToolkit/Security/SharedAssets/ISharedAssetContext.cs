namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Host-neutral access to the shared asset of the current context. Replaces the former "reach into
    /// <c>Request.Query</c> (or, failing that, into the <c>Referer</c>)" that every consumer used to do on
    /// its own: the key now travels as a path segment, and a Blazor circuit - which has neither a query nor
    /// a usable referer per navigation - can answer from its base URI.
    /// </summary>
    public interface ISharedAssetContext
    {
        /// <summary>
        /// Gets a value indicating whether the current context runs inside a shared asset.
        /// </summary>
        bool HasAsset { get; }

        /// <summary>
        /// Gets the key of the asset the current context runs in, or null.
        /// </summary>
        string AssetKey { get; }

        /// <summary>
        /// Gets the anonymous access-token of the current context, or null. Present only for links created
        /// for anonymous recipients.
        /// </summary>
        string AccessToken { get; }

        /// <summary>
        /// Gets the raw path segment (with marker, without slashes) of the current asset, or null. This is
        /// what link-building prepends; see <see cref="SharedAssetPath.BuildPrefix"/>.
        /// </summary>
        string Segment { get; }
    }
}
