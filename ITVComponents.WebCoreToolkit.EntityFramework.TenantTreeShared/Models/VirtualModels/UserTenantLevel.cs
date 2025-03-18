using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels
{
    public class UserTenantLevel<TUser>
    where TUser:class
    {
        public int Level { get; set; }

        public int TenantId { get; set; }

        public int RoleId { get; set; }

        public TUser User { get; set; }
    }
}
