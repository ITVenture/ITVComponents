using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Security
{
    public interface IScriptingRestriction
    {
        PolicyMode PolicyMode { get; }

        IScriptingRestriction Clone();
    }
}
