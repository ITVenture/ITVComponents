using System;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>
    /// Reads the shared-asset of the current circuit for the file-transfer components, so they can hand
    /// <c>hasAsset</c>/<c>assetKey</c> to <c>IFileServiceHandler</c> the way the MVC edge already does.
    /// </summary>
    /// <remarks>
    /// <b>Resolved optionally, on purpose.</b> <see cref="ISharedAssetContext"/> is registered by
    /// <c>UseSharedAssetPathContext</c> (directly, or implied by <c>UseAssetDrivenClaimsTransformation</c> /
    /// <c>UseSharedAssets</c>) — a host that does not use shared assets at all has no such registration, and a
    /// hard <c>@inject</c> in the shared components would break every download there with a resolution error.
    /// </remarks>
    internal static class SharedAssetCallContext
    {
        /// <summary>
        /// Determines whether the current context runs inside a shared asset, and under which key.
        /// </summary>
        /// <param name="services">the service-provider of the current component</param>
        /// <returns>
        /// the asset-flag and the asset-key; <c>(false, null)</c> for a signed-in user outside any share and
        /// for a host that does not use shared assets at all — which is exactly today's call.
        /// </returns>
        public static (bool HasAsset, string? AssetKey) Resolve(IServiceProvider? services)
        {
            var assetContext = services?.GetService<ISharedAssetContext>();
            if (assetContext is not { HasAsset: true })
            {
                return (false, null);
            }

            return (true, assetContext.AssetKey);
        }
    }
}
