using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.HelperModels.Comparers
{
    public class WebPluginConstantComparer:IEqualityComparer<WebPluginConstant>
    {
        public bool Equals(WebPluginConstant x, WebPluginConstant y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Name == y.Name;
        }

        public int GetHashCode(WebPluginConstant obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
