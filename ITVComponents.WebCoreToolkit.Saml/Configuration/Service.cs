using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;

namespace ITVComponents.WebCoreToolkit.Saml.Configuration
{
    public class Service
    {
        public string Name { get; set; }

        public bool IsDefault { get; set; }

        public string Language { get; set; }

        [AutoResolveChildren]
        public List<RequestedAttribute> RequestedAttributes { get; set; } = new ();
    }
}
