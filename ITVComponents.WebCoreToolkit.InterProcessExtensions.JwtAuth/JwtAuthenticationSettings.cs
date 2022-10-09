using ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.JwtAuth
{
    public class JwtAuthenticationSettings
    {
        public JwtAuthConfigCollection JwtAuthSchemes { get; set; } = new();
    }
}
