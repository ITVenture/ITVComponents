using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Security.Restrictions
{
    public class MethodAccessRestriction:IScriptingRestriction
    {
        public PolicyMode PolicyMode { get; internal set; }
        public IScriptingRestriction Clone()
        {
            return new MethodAccessRestriction
            {
                PolicyMode = PolicyMode,
                Method = Method
            };
        }

        public MethodInfo Method { get; internal set; }
    }
}
