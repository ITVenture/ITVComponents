using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.Comparers
{
    public class WebPluginComparer : IEqualityComparer<WebPlugin>
    {
        public bool Equals(WebPlugin x, WebPlugin y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.UniqueName == y.UniqueName;
        }

        public int GetHashCode(WebPlugin obj)
        {
            return (obj.UniqueName != null ? obj.UniqueName.GetHashCode() : 0);
        }
    }
}
