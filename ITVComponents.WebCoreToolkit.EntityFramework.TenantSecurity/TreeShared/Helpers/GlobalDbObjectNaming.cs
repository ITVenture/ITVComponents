using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers
{
    public static class GlobalDbObjectNaming
    {
        public const string UpwardsTenantTreeView = "UpwardsTenantTree";
        public const string DownwardsTenantTreeView = "DownwardsTenantTree";
        public const string UserAccessTree = "TenantAccessTree";
        /*public const string UpwardsRoleTreeView = "UpwardsRoleTree";
        public const string DownwardsRoleTreeView = "DownwardsRoleTree";*/
        //public const string Get
        public const string ChildTenantsWithProc = "ChildTenantsWith";
    }
}
