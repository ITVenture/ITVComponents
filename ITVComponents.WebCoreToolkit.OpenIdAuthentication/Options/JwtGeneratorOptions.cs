using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class JwtGeneratorOptions
    {
        public string Issuer { get; set; }
        public string IssuerKey { get; set; }
        public string Audience { get; set; }
        public int TokenDuration { get; set; }
        public List<ClaimData> IncludedClaims { get; set; } = new ();
        public bool ValidateIssuer { get; set; }
        public bool ValidateAudience { get; set; }
        public bool ValidateIssuerSigningKey { get; set; }
        public string ValidAudience { get; set; }
        public string ValidIssuer { get; set; }
        public string ApplicationKeyClaim { get; set; }
    }
}
