using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class BearerConnectOptions
    {
        public string Name { get; set; }
        public string AuthenticationScheme { get; set; } = JwtBearerDefaults.AuthenticationScheme;

        public string Authority { get; set; }
    }
}
