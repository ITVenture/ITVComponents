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

namespace ITVComponents.WebCoreToolkit.Security.ClaimsTransformation
{
    public class AssetDrivenClaimsTransformation : ICollectedClaimsProvider
    {
        private readonly IContextUserProvider userProvider;
        private readonly IServiceScopeFactory serviceProvider;
        public const string ITVentureIssuerString = "IT-Venture WebCore-Toolkit -- Shared Assets";

        /// <summary>
        /// Initializes a new instance of the AssetDrivenClaimsTransformation class
        /// </summary>
        /// <param name="userProvider">provides access to the current http-context</param>
        /// <param name="serviceProvider">the service-provider that enables this object to get registered services</param>
        public AssetDrivenClaimsTransformation(IContextUserProvider userProvider, IServiceScopeFactory serviceProvider)
        {
            this.userProvider = userProvider;
            this.serviceProvider = serviceProvider;
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
            IQueryCollection refQ;
            if (userProvider.HttpContext != null &&
                ((refQ = userProvider.HttpContext.Request.Query).ContainsKey(Global.FixedAssetRequestQueryParameter)
                || (refQ = userProvider.HttpContext.Request.GetRefererQuery()) != null && refQ.ContainsKey(Global.FixedAssetRequestQueryParameter)))
            {
                var assetKey = refQ[Global.FixedAssetRequestQueryParameter];
                using (var context = serviceProvider.CreateScope())
                {
                    var assetManager = context.ServiceProvider.GetService<ISharedAssetAdapter>();
                    var assetInfo = assetManager.GetAssetInfo(assetKey, principal);
                    var id = principal.Identity as ClaimsIdentity;
                    id.AddClaims(from t in assetInfo.Permissions select new Claim(ClaimTypes.FixedAssetPermission, t));
                    id.AddClaims(from t in assetInfo.Features select new Claim(ClaimTypes.FixedAssetFeature, t));
                    id.AddClaim(new Claim(ClaimTypes.FixedUserScope, assetInfo.UserScopeName));
                }
            }

            return Task.FromResult(principal);
        }
    }
}
