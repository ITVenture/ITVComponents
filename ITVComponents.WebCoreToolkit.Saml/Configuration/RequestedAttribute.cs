using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Saml.Configuration
{
    public class RequestedAttribute
    {
        public string Name { get; set; }
        public string FriendlyName { get; set; }
        public bool? IsRequired { get; set; }
        public string NameFormat { get; set; }
    }
}
