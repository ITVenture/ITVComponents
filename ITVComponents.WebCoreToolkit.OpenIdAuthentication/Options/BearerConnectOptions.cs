using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class BearerConnectOptions
    {
        public string Name { get; set; }

        public string Authority { get; set; }
        public TokenValidationParameterOptions TokenValidationParameters { get; set; }
        public  bool RequireHttpsMetadata { get; set; }
        public bool SaveToken { get; set; }
        public bool UseTokenGenerator { get; set; }
        public int TokenDuration { get; set; }
        public string EventHandler { get; set; }
        public List<ClaimData> Claims { get; set; } = new();
        public bool MapApplicationId { get; set; }
        public bool ExposeTokenEndpoints { get; set; }
        public string ApplicationIdClaim { get; set; }
        public string? AuthSchemeExtension { get; set; }
        public bool? Selectable { get; set; }
        public string LogoFile { get; set; }
    }
}
 