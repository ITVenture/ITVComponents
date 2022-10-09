using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Security.Restrictions
{
    public class AssemblyRestriction:IScriptingRestriction
    {
        public PolicyMode PolicyMode { get; internal set; }
        public IScriptingRestriction Clone()
        {
            return new AssemblyRestriction { PolicyMode = PolicyMode };
        }

        public string AssemblyName { get; internal set; }
    }
}
