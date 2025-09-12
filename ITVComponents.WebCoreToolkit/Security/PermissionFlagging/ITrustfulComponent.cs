using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    public interface ITrustfulComponent
    {
        protected internal bool IsComponentSecureFor(string topic);
    }
}
