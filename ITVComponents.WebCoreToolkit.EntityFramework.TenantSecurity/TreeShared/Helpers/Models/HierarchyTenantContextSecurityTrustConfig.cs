using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models
{
    public class HierarchyTenantContextSecurityTrustConfig<TTrustConfig>:Shared.Helpers.Models.BaseTenantContextSecurityTrustConfig<TTrustConfig>
    where TTrustConfig: HierarchyTenantContextSecurityTrustConfig<TTrustConfig>, new()
    {
        public bool IncludeParentTree { get; set; }
        public bool IncludeChildTree { get; set; }

        protected override TTrustConfig Clone(TTrustConfig other)
        {
            var retVal = base.Clone(other);
            retVal.IncludeChildTree = IncludeChildTree;
            retVal.IncludeParentTree = IncludeParentTree;
            return retVal;
        }
    }

    public class
        HierarchyTenantContextSecurityTrustConfig : HierarchyTenantContextSecurityTrustConfig<
        HierarchyTenantContextSecurityTrustConfig>
    {
    }
}
