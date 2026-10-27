using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions
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
                TokenRequestParameters = definition.TokenParameters.ToTokenRequestParameters(clientSecretAsIndicators),
                SecretTokenRequestParameters = definition.TokenParameters?.Where(n => n.IsSecret)
                    .Select(n => n.ParameterName).ToArray() ?? Array.Empty<string>()
            };
        }

        /// <summary>
        /// Converts the configured token-parameters of a service into the representation of an ExternalServiceConnection
        /// </summary>
        /// <param name="parameters">the parameters of a service (may be null)</param>
        /// <param name="secretsAsIndicators">
        /// true for display: secret values become <c>--ENCRYPTED</c> / <c>--EMPTY--</c>, like the ClientSecret
        /// </param>
        /// <returns>the parameters as key-value pairs, ordered by name; secret values still encrypted</returns>
        public static KeyValuePair<string, string>[] ToTokenRequestParameters(this IEnumerable<ExternalOAuthServiceParameter> parameters, bool secretsAsIndicators = false)
        {
            return parameters?.OrderBy(n => n.ParameterName, StringComparer.OrdinalIgnoreCase)
                .Select(n => new KeyValuePair<string, string>(n.ParameterName,
                    secretsAsIndicators && n.IsSecret
                        ? (string.IsNullOrEmpty(n.ParameterValue) ? "--EMPTY--" : "--ENCRYPTED")
                        : n.ParameterValue))
                .ToArray() ?? Array.Empty<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Applies what an admin view entered to a token parameter - the one rule both admin views follow.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>Secret, value entered: it is encrypted.</item>
        /// <item>Secret, nothing entered: an existing secret is kept; a plain value that is being marked secret is
        /// encrypted as it is.</item>
        /// <item>Not secret: the entered value is stored as plain text - except when the parameter WAS secret and nothing
        /// was entered. A secret is never turned back into plain text silently; it has to be entered again.</item>
        /// </list>
        /// </remarks>
        /// <param name="parameter">the parameter to change</param>
        /// <param name="isSecret">whether it is to be secret</param>
        /// <param name="enteredValue">what was entered; empty for a secret means "keep"</param>
        /// <param name="encrypt">encrypts with the key of the service; returns null when that key is not available</param>
        /// <returns>whether the value was applied, or why not</returns>
        public static TokenParameterValueResult ApplyValue(this ExternalOAuthServiceParameter parameter, bool isSecret,
            string enteredValue, Func<string, string> encrypt)
        {
            if (isSecret)
            {
                var plain = !string.IsNullOrEmpty(enteredValue)
                    ? enteredValue
                    : !parameter.IsSecret ? parameter.ParameterValue : null;
                if (!string.IsNullOrEmpty(plain))
                {
                    var encrypted = encrypt(plain);
                    if (encrypted == null)
                    {
                        return TokenParameterValueResult.EncryptionFailed;
                    }

                    parameter.ParameterValue = encrypted;
                }

                parameter.IsSecret = true;
                return TokenParameterValueResult.Applied;
            }

            if (parameter.IsSecret && !string.IsNullOrEmpty(parameter.ParameterValue) && string.IsNullOrEmpty(enteredValue))
            {
                return TokenParameterValueResult.SecretMustBeEnteredAgain;
            }

            parameter.ParameterValue = enteredValue;
            parameter.IsSecret = false;
            return TokenParameterValueResult.Applied;
        }
    }

    /// <summary>The outcome of <see cref="OAuthExtensions.ApplyValue"/>.</summary>
    public enum TokenParameterValueResult
    {
        /// <summary>The value (and flag) was applied.</summary>
        Applied,

        /// <summary>A secret was to become plain text without the value being entered again.</summary>
        SecretMustBeEnteredAgain,

        /// <summary>The key of the service was not available (its tenant was not found).</summary>
        EncryptionFailed
    }
}
