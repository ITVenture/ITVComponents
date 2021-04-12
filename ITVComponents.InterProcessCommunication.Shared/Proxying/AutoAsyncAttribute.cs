using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.Proxying
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AutoAsyncAttribute:Attribute
    {
        public string SyncMethodName { get; }

        public AutoAsyncAttribute(string syncMethodName)
        {
            SyncMethodName = syncMethodName;
        }
    }
}
