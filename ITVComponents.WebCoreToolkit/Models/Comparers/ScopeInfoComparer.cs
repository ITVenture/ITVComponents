using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.Comparers
{
    public class ScopeInfoComparer:IEqualityComparer<ScopeInfo>
    {
        public bool Equals(ScopeInfo x, ScopeInfo y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.ScopeName == y.ScopeName;
        }

        public int GetHashCode(ScopeInfo obj)
        {
            return (obj.ScopeName != null ? obj.ScopeName.GetHashCode() : 0);
        }
    }
}
