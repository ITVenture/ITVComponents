using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public abstract class ExternalAuthenticationOptionsBase
    {
        public ICollection<string> Scope { get; set; } = new List<string>();
        public string ClientSecret { get; set; }
        public string ClientId { get; set; }
        public string SignInScheme { get; set; }
        public bool SaveTokens { get; set; }
        public string? Name { get; set; }
    }
}
