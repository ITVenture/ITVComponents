using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.Comparers
{
    public class WebPluginParameterComparer : IEqualityComparer<WebPluginGenericParam>
    {
        public bool Equals(WebPluginGenericParam x, WebPluginGenericParam y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.GenericTypeName == y.GenericTypeName;
        }

        public int GetHashCode(WebPluginGenericParam obj)
        {
            return obj.GenericTypeName.GetHashCode();
        }
    }
}
