using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;

namespace ITVComponents.WebCoreToolkit.Saml.Configuration
{
    public class SamlOptions
    {
        public string EntityId { get; set; }


        [AutoResolveChildren]
        public ServiceCompatibility Compatibility { get; set; }

        [AutoResolveChildren]
        public DefaultCompany DefaultCompany { get; set; } = new();

        [AutoResolveChildren]
        public List<Contact> Contacts { get; set; } = new ();

        [AutoResolveChildren]
        public List<Service> Services { get; set; } = new();

        public bool RequestsSigned => SignRequests?.ToLower() == "true";

        public string SignRequests { get; set; }
        
        [AutoResolveChildren]
        public List<SignCertificate> SignCertificates { get; set; } = new();

        public string AuthenticateRequestSigningBehavior { get; set; }
        public string NameIdPolicy { get; set; }
        public bool WantAssertionsSigned  => RequireAssertionsSigned?.ToLower() == "true";

        public string RequireAssertionsSigned { get; set; }

        [AutoResolveChildren]
        public List<SamlIdentityProvider> IdentityProviders { get; set; } = new();
    }
}
