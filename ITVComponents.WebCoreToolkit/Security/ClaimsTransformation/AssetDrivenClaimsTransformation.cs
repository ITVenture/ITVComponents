using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Security.ClaimsTransformation
{
    /// <summary>
    /// Decorates the current principal with the fixed scope, permissions and features of the shared asset
    /// the request runs in.
    /// <para>
    /// Reads the asset through <see cref="ISharedAssetContext"/> instead of digging in the query string
    /// itself, which is what makes it work in a Blazor circuit as well: there is no query and no usable
    /// referer per navigation there, but there is a path prefix.
    /// </para>
    /// </summary>
    public class AssetDrivenClaimsTransformation : ICollectedClaimsProvider
    {
        private readonly ISharedAssetContext assetContext;
        private readonly IServiceScopeFactory serviceProvider;
        private readonly ILogger<AssetDrivenClaimsTransformation> logger;
        public const string ITVentureIssuerString = "IT-Venture WebCore-Toolkit -- Shared Assets";

        /// <summary>
        /// Initializes a new instance of the AssetDrivenClaimsTransformation class
        /// </summary>
        /// <param name="assetContext">provides the shared asset of the current context</param>
        /// <param name="serviceProvider">the service-provider that enables this object to get registered services</param>
        /// <param name="logger">a logger for assets that can not be applied</param>
        public AssetDrivenClaimsTransformation(ISharedAssetContext assetContext, IServiceScopeFactory serviceProvider,
            ILogger<AssetDrivenClaimsTransformation> logger)
        {
            this.assetContext = assetContext;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        /// <summary>
        /// Provides a central transformation point to change the specified principal.
        /// Note: this will be run on each AuthenticateAsync call, so its safer to
        /// return a new ClaimsPrincipal if your transformation is not idempotent.
        /// </summary>
        /// <param name="principal">The <see cref="T:System.Security.Claims.ClaimsPrincipal" /> to transform.</param>
        /// <returns>The transformed principal.</returns>
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (!assetContext.HasAsset || principal?.Identity is not ClaimsIdentity id)
            {
                return Task.FromResult(principal);
            }

            if (id.HasClaim(n => n.Type == ClaimTypes.FixedUserScope))
            {
                // Idempotenz: die Transformation laeuft bei jedem AuthenticateAsync, die Claims sind aber
                // schon dran. Ohne diese Probe sammelt derselbe Prinzipal sie mehrfach ein.
                return Task.FromResult(principal);
            }

            using (var context = serviceProvider.CreateScope())
            {
                var assetManager = context.ServiceProvider.GetService<ISharedAssetAdapter>();
                if (assetManager == null)
                {
                    logger.LogError(
                        "A request runs inside shared asset '{AssetKey}', but no ISharedAssetAdapter is registered - no asset permissions are applied.",
                        assetContext.AssetKey);
                    return Task.FromResult(principal);
                }

                var assetInfo = assetManager.GetAssetInfo(assetContext.AssetKey, principal);
                if (assetInfo == null)
                {
                    // Kein Zugriff (Filter, Gueltigkeitsfenster, unbekannter Schluessel). Das ist eine
                    // legitime Antwort - aber eine, die man im Log sehen muss, weil der Besucher nur eine
                    // Seite ohne Inhalt sieht und "der Link geht nicht" meldet.
                    logger.LogInformation(
                        "Shared asset '{AssetKey}' is not accessible for the current requestor; no asset claims are applied.",
                        assetContext.AssetKey);
                    return Task.FromResult(principal);
                }

                id.AddClaims(from t in assetInfo.Permissions select new Claim(ClaimTypes.FixedAssetPermission, t));
                id.AddClaims(from t in assetInfo.Features select new Claim(ClaimTypes.FixedAssetFeature, t));
                id.AddClaim(new Claim(ClaimTypes.FixedUserScope, assetInfo.UserScopeName));
            }

            return Task.FromResult(principal);
        }
    }
}
