using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options
{
    public class FacebookConnectOptions
    {
        public string AppId { get; set; }

        public string AppSecret { get; set; }

        public string AccessDeniedPath { get; set; }
        public string? AuthSchemeExtension { get; set; }

        public string? DisplayName { get; set; }
        public bool? Selectable { get; set; }
        public string LogoFile { get; set; }
    }
}
