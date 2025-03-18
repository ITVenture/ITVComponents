using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models
{
    public class HierarchyTenantContextSecurityTrustConfig:TenantSecurityShared.Helpers.Models.BaseTenantContextSecurityTrustConfig
    {
        public bool IncludeParentTree { get; set; }
        public bool IncludeChildTree { get; set; }
    }
}
