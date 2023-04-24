using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class OpenIdConnectOptions:ExternalAuthenticationOptionsBase
    {
        public string MetadataAddress { get; set; }
        public string SignedOutRedirectUri { get; set; }
        public string ResponseType { get; set; }
        public bool GetClaimsFromUserInfoEndpoint { get; set; }

        public bool RequireHttpsMetadata { get; set; }

        public bool TokenToClaims { get; set; }
        public bool ShrinkStatus { get; set; }

        public string AccessDeniedPath { get; set; }
    }
}
