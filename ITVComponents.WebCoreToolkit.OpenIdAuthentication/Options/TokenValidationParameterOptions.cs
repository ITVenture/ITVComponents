using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class TokenValidationParameterOptions
    {
        public bool ValidateIssuer { get; set; }
        public bool ValidateAudience { get; set; }
        public bool  ValidateLifetime { get; set; }
        public bool ValidateIssuerSigningKey { get; set; }
        public double ClockSkew { get; set; }
        public string ValidAudience { get; set; }
        public string ValidIssuer { get; set; }
        public string IssuerSigningKey { get; set; }
    }
}
