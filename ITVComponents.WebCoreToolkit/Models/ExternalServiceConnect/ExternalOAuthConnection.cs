using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect
{
    public class ExternalServiceConnection
    {
        public string UniqueConnectionName { get; set; }

        public string AuthorizationEndpoint { get; set; } = null!;
        public string TokenEndpoint { get; set; } = null!;
        public string RevocationEndpoint { get; set; } = null!;
        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;

        public string Scope { get; set; } = null!;

        public bool Global { get; set; }
        public string GlobalUniqueConnectionName { get; set; }

        public ExternalServiceAuthenticationType AuthenticationType { get; set; }

        /// <summary>
        /// Additional form-parameters for the token-request. Treat as read-only: connection objects are flat-copied from
        /// buffered instances, so the array may be shared.
        /// </summary>
        public KeyValuePair<string, string>[] TokenRequestParameters { get; set; } = Array.Empty<KeyValuePair<string, string>>();

        /// <summary>
        /// The names of the <see cref="TokenRequestParameters"/> whose values are stored encrypted. Until
        /// <see cref="DecryptTokenRequestParameters"/> ran, those values are the cipher text.
        /// </summary>
        public string[] SecretTokenRequestParameters { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Replaces the encrypted values in <see cref="TokenRequestParameters"/> by their plain text - with the same
        /// decryption as the ClientSecret of this connection.
        /// </summary>
        /// <remarks>
        /// Builds a NEW array: connection objects are flat-copied from buffered instances, and decrypting the shared
        /// array in place would put plain text into the buffer.
        /// </remarks>
        /// <param name="decrypt">decrypts one value</param>
        public void DecryptTokenRequestParameters(Func<string, string> decrypt)
        {
            if (SecretTokenRequestParameters == null || SecretTokenRequestParameters.Length == 0 || TokenRequestParameters == null)
            {
                return;
            }

            var secrets = new HashSet<string>(SecretTokenRequestParameters, StringComparer.OrdinalIgnoreCase);
            TokenRequestParameters = TokenRequestParameters
                .Select(n => secrets.Contains(n.Key) && !string.IsNullOrEmpty(n.Value)
                    ? new KeyValuePair<string, string>(n.Key, decrypt(n.Value))
                    : n)
                .ToArray();
        }
    }

    public enum ExternalServiceAuthenticationType
    {
        OAuthAuthorizationFlow,
        OAuthClientCredentialsFlow,
        OAuthBasicClientCredentialsFlow,
        Basic,
        ApiKey,
        BearerToken,
        OAuthPasswordFlow
    }
}
