using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class ExternalOAuthServiceViewModel
    {
        public int OAuthServiceId { get; set; }
        public string UniqueConnectionName { get; set; }

        public string AuthorizationEndpoint { get; set; } = null!;
        public string TokenEndpoint { get; set; } = null!;
        public string RevocationEndpoint { get; set; } = null!;
        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;

        public string Scope { get; set; } = null!;

        public bool Global { get; set; }
        public bool Editable { get; set; }
        public bool IsConnected { get; set; }

        public  ExternalServiceAuthenticationType AuthenticationType { get; set; }
    }
}
