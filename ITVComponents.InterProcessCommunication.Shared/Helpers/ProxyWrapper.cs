using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    public class ProxyWrapper
    {
        public object Value { get; set; }

        public IIdentity Owner { get; set; }
    }
}
