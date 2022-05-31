using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Options
{
    public class WebPartOptions
    {
        public string AuthenticationType { get; set; } = ApiKeyAuthenticationOptions.DefaultScheme;
    }
}
