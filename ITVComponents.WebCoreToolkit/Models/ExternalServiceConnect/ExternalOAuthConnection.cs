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
