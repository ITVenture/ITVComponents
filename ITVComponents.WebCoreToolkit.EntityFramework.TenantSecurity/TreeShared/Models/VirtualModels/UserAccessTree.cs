using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels
{
    public class UserAccessTree<TUserId>
    {
        public int OutermostLeafTenantId { get; set; }

        public string OutermostLeafTenantName { get; set; }

        public int ParentTenantId { get; set; }

        public string ParentTenantName { get; set; }

        public int ParentLevel { get; set; }

        public int TopmostTenantId { get; set; }

        public string TopmostTenantName { get; set; }

        public int ChildTenantId { get; set; }

        public string ChildTenantName { get; set; }

        public int TenantUserId { get; set; }

        public TUserId UserId { get; set; }

        public bool DirectAssign { get; set; }
    }
}
