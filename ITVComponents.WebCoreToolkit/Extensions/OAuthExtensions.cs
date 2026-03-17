using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class OAuthExtensions
    {
        public static string RedirectUri(this ExternalServiceConnection connection)
        {
            return $"/XSvcAuth/callback/{Uri.EscapeDataString(connection.GlobalUniqueConnectionName)}";
        }
    }
}
