using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect
{
    /// <summary>
    /// Describes which additional token-request parameters are applicable for an authentication-type
    /// </summary>
    public static class TokenRequestParameterRules
    {
        /// <summary>
        /// Indicates whether additional token-request parameters are sent for the given authentication-type
        /// </summary>
        /// <param name="authenticationType">the authentication-type of a connection</param>
        /// <returns>true if the type requests its token with a form-post that accepts additional parameters</returns>
        public static bool SupportsParameters(ExternalServiceAuthenticationType authenticationType)
        {
            return authenticationType is ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
                or ExternalServiceAuthenticationType.OAuthBasicClientCredentialsFlow
                or ExternalServiceAuthenticationType.OAuthPasswordFlow;
        }

        /// <summary>
        /// Gets the parameter-names the given authentication-type sets itself. These can not be overridden by additional parameters
        /// </summary>
        /// <param name="authenticationType">the authentication-type of a connection</param>
        /// <returns>the reserved parameter-names (compare case-insensitive)</returns>
        public static string[] ReservedNames(ExternalServiceAuthenticationType authenticationType)
        {
            return authenticationType switch
            {
                ExternalServiceAuthenticationType.OAuthClientCredentialsFlow => new[] { "grant_type", "client_id", "client_secret" },
                ExternalServiceAuthenticationType.OAuthPasswordFlow => new[] { "grant_type", "username", "password" },
                _ => new[] { "grant_type" }
            };
        }

        /// <summary>
        /// Indicates whether the given name is reserved for the given authentication-type
        /// </summary>
        /// <param name="authenticationType">the authentication-type of a connection</param>
        /// <param name="parameterName">the name of the additional parameter</param>
        /// <returns>true if the parameter is set by the authentication-type itself</returns>
        public static bool IsReserved(ExternalServiceAuthenticationType authenticationType, string parameterName)
        {
            return ReservedNames(authenticationType).Contains(parameterName?.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
