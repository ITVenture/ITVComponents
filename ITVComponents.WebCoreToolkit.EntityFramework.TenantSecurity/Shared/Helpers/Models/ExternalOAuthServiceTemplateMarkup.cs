using System.Text.Json.Serialization;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(ExternalOAuthServiceTemplateMarkup), "base")]
    public class ExternalOAuthServiceTemplateMarkup
    {
        public string UniqueConnectionName { get; set; }

        public string AuthorizationEndpoint { get; set; }

        public string TokenEndpoint { get; set; }

        public string RevocationEndpoint { get; set; }

        public string ClientId { get; set; }

        public string Scope { get; set; }

        public bool Global { get; set; }

        public ExternalServiceAuthenticationType AuthenticationType { get; set; }

        /// <summary>
        /// The additional token-request parameters. A secret parameter travels with its name and flag only - its value
        /// stays on the system it was entered on, like the ClientSecret.
        /// </summary>
        public ExternalOAuthServiceParameterTemplateMarkup[] TokenParameters { get; set; }

        // NOTE: ClientSecret is deliberately NOT part of the template — secrets must not travel with a tenant
        // template (which may be serialized/stored). On apply the service is created with an empty secret; the
        // tenant admin sets it afterwards.
    }
}
