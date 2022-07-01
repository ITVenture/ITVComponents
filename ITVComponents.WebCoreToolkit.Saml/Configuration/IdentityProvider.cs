using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Saml.Configuration
{
    public class SamlIdentityProvider
    {
        public string EntityId { get; set; }
        public bool LoadMetadata { get; set; }
        public string MetadataLocation { get; set; }
    }
}
