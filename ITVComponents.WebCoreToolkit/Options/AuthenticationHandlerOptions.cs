using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class AuthenticationHandlerOptions
    {
        public List<AuthenticationHandlerDefinition> AuthenticationHandlers { get; set; } =
            new List<AuthenticationHandlerDefinition>();
    }

    public class AuthenticationHandlerDefinition
    {
        public string AuthenticationSchemeName { get; set; }

        public string DisplayName { get; set; }

        public string LogoFile { get; set; }

        public bool DisplayInHandlerSelection { get; set; }
    }
}
