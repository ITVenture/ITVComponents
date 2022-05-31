using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class TrustedModuleTemplateMarkup
    {
        public string FullQualifiedTypeName { get; set; }
        public string Description { get; set; }
        public bool TrustedForAllTenants { get; set; }
        public bool TrustedForGlobals { get; set; }
    }
}
