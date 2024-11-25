using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers
{
    public static class GlobalDbObjectNaming
    {
        public const string UpwardsTenantTreeView = "UpwardsTenantTree";
        public const string DownwardsTenantTreeView = "DownwardsTenantTree";
        public const string UpwardsPermissionTreeView = "UpwardsPermissionTree";
        public const string DownwardsPermissionTreeView = "DownwardsPermissionTree";
        public const string ChildTenantsWithProc = "ChildTenantsWith";
    }
}
