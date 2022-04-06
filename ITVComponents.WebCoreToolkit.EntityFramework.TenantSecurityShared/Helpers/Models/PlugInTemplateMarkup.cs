using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class PlugInTemplateMarkup
    {
        public string UniqueName { get; set; }

        public string Constructor { get; set; }

        public bool AutoLoad { get; set; }

        public PlugInGenericArgumentTemplateMarkup[] GenericArguments { get; set; }
    }
}
