using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Security.Restrictions
{
    public class EventAccessRestriction: IScriptingRestriction
    {
        public PolicyMode PolicyMode { get; internal set; }
        public IScriptingRestriction Clone()
        {
            return new EventAccessRestriction { PolicyMode = PolicyMode, Event = Event };
        }

        public EventInfo Event { get; internal set; }
    }
}
