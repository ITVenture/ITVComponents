namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Creates the access-token that lets an anonymous recipient use a shared asset.
    /// <para>
    /// Formerly this appended the token to a finished URL as a query parameter. It now returns the token
    /// alone: where it goes is the business of <see cref="SharedAssetPath"/>, which puts it into the path
    /// segment together with the asset key - one place that knows the URL shape.
    /// </para>
    /// </summary>
    public interface IAnonymousAssetLinkProvider
    {
        /// <summary>
        /// Creates the anonymous access-token for the given asset. The token carries the asset's raw token,
        /// the moment the link was created and the validity window, so a link can be invalidated by
        /// changing the window without touching the asset key.
        /// </summary>
        /// <param name="info">the asset to create the token for</param>
        /// <returns>the Base64Url-encoded token</returns>
        string CreateAnonymousToken(FullAssetInfo info);
    }
}
