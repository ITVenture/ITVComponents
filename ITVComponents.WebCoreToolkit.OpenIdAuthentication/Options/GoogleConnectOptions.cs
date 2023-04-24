using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class GoogleConnectOptions:ExternalAuthenticationOptionsBase
    {
        public string AccessType { get; set; }

        public string AccessDeniedPath { get; set; }
    }
}
