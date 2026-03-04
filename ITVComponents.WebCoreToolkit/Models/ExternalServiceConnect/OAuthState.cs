using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect
{
    public class OAuthState
    {
        public string State { get; set; } = null!;
        public string ConnectionName { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }

        public string CodeVerifier { get; set; } = null!;
        public bool ScopeSwitchRequired { get; set; }
        public string ExplicitScope { get; set; }
    }
}
