using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.Comparers
{
    public class PermissionComparer:IEqualityComparer<Permission>
    {
        public bool Equals(Permission x, Permission y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.PermissionName.Equals(y.PermissionName,StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Permission obj)
        {
            return (obj.PermissionName != null ? obj.PermissionName.GetHashCode() : 0);
        }
    }
}
