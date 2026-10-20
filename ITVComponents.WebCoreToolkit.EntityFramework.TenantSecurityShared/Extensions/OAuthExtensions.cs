using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class OAuthExtensions
    {
        public static ExternalServiceConnection ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>(
            this TExternalOAuthService definition, bool clientSecretAsIndicators = false) 
            where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin> 
            where TTenant : Tenant where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin> 
            where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        {
            return new ExternalServiceConnection
            {
                AuthorizationEndpoint = definition.AuthorizationEndpoint,
                ClientId = definition.ClientId,
                ClientSecret = !clientSecretAsIndicators
                    ? definition.ClientSecret
                    : (string.IsNullOrEmpty(definition.ClientSecret) ? "--EMPTY--" : "--ENCRYPTED"),
                Global = definition.Global,
                GlobalUniqueConnectionName = definition.CalculatedUniqueServiceName,
                RevocationEndpoint = definition.RevocationEndpoint,
                Scope = definition.Scope,
                TokenEndpoint = definition.TokenEndpoint,
                UniqueConnectionName = definition.UniqueConnectionName,
                AuthenticationType = definition.AuthenticationType,
                TokenRequestParameters = definition.TokenParameters.ToTokenRequestParameters()
            };
        }

        /// <summary>
        /// Converts the configured token-parameters of a service into the representation of an ExternalServiceConnection
        /// </summary>
        /// <param name="parameters">the parameters of a service (may be null)</param>
        /// <returns>the parameters as key-value pairs, ordered by name</returns>
        public static KeyValuePair<string, string>[] ToTokenRequestParameters(this IEnumerable<ExternalOAuthServiceParameter> parameters)
        {
            return parameters?.OrderBy(n => n.ParameterName, StringComparer.OrdinalIgnoreCase)
                .Select(n => new KeyValuePair<string, string>(n.ParameterName, n.ParameterValue))
                .ToArray() ?? Array.Empty<KeyValuePair<string, string>>();
        }
    }
}
