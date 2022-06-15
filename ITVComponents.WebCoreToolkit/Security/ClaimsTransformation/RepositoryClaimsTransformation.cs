using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Security.ClaimsTransformation
{
    public class RepositoryClaimsTransformation:ICollectedClaimsProvider
    {
        public const string ITVentureIssuerString = "IT-Venture WebCore-Toolkit";

        private readonly ISecurityRepository securityRepository;
        private readonly IServiceScopeFactory serviceProvider;

        /// <summary>
        /// Initializes a new instance of the SettingsClaimsTransformation class
        /// </summary>
        /// <param name="securityRepository">the configured user-repository</param>
        /// <param name="serviceProvider">the service-provider that enables this object to get registered services</param>
        public RepositoryClaimsTransformation(ISecurityRepository securityRepository, IServiceScopeFactory serviceProvider)
        {
            this.securityRepository = securityRepository;
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
            using (var context = serviceProvider.CreateScope())
            {
                foreach (var id in principal.Identities)
                {
                    var userNameMapper = context.ServiceProvider.GetService<IUserNameMapper>();
                    var userLabels = userNameMapper.GetUserLabels(id);
                    id.AddClaims(from p in securityRepository.GetCustomProperties(userLabels, id.AuthenticationType)
                        select new Claim(p.PropertyName, p.Value, ClaimValueTypes.String, ITVentureIssuerString));
                    id.AddClaims(
                        from p in securityRepository.GetCustomProperties(id.GetClaimData(), id.AuthenticationType)
                        select new Claim(p.Type, p.Value, p.ValueType ?? ClaimValueTypes.String,
                            p.Issuer ?? ITVentureIssuerString, p.OriginalIssuer));
                }
                
                return Task.FromResult(principal);
            }
        }
    }
}
