using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    public class ActiveCodeAccessDescriptor
    {
        public object BaseObject { get; set; }

        public string Name { get;set; }
        public bool WeakAccess { get; set; }

        public Type ExplicitType { get; set; }

        public object[] Arguments { get; set; }
    }
}
