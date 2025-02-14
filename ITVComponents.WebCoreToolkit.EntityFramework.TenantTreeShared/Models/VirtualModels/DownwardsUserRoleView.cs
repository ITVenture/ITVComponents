using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels
{
    public class DownwardsUserRoleView<TUserId>
    {
        public TUserId UserId { get; set; }

        public int TopmostTenantId { get; set; }

        public string TopmostTenantName { get; set; }

        public int TopmostParentLevel { get; set; }

        public int ViewPointTenantId { get; set; }

        public string ViewpointTenantName { get; set; }

        public int ChildTenantId { get; set; }

        public string ChildTenantName { get; set; }

        public int ChildLevel { get; set; }

        public int TenantUserId { get; set; }

        public int ResultingChildRoleId { get; set; }
    }
}
