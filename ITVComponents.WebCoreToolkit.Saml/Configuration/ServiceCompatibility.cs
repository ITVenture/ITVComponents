using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Saml.Configuration
{
    public class ServiceCompatibility
    {
        public bool AcceptUnsignedLogoutResponses { get; set; }
        public bool StrictOwinAuthenticationMode { get; set; }
        public bool UnpackEntitiesDescriptorInIdentityProviderMetadata { get; set; }
    }
}
