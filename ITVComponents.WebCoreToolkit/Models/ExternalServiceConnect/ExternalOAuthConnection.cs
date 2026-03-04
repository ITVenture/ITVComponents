using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect
{
    public class ExternalOAuthConnection
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
    }
}
