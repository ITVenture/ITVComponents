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
                AuthenticationType = definition.AuthenticationType
            };
        }
    }
}
