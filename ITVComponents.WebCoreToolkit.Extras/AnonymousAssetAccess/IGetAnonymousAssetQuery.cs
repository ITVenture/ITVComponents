using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Models;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess
{
    /// <summary>
    /// Validates the anonymous access to a shared asset. Source-neutral by intent: where key and token came
    /// from - the path segment of the request or the deprecated query - is the caller's business, not this
    /// contract's.
    /// </summary>
    public interface IGetAnonymousAssetQuery
    {
        /// <summary>
        /// Checks whether the given key/token pair grants anonymous access to a shared asset.
        /// </summary>
        /// <param name="assetKey">the key of the requested asset</param>
        /// <param name="accessToken">the access-token that came with it</param>
        /// <param name="denied">
        /// true when a key/token pair WAS provided but did not check out - which is a different answer from
        /// "no anonymous asset in this request" and must not be silently turned into one.
        /// </param>
        /// <returns>the anonymous asset when access is granted, otherwise null</returns>
        AnonymousAsset Execute(string assetKey, string accessToken, out bool denied);
    }
}
