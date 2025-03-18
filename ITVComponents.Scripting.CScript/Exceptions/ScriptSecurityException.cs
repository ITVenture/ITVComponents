using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Exceptions
{
    public class ScriptSecurityException:ScriptException
    {
        public ScriptSecurityException()
        {
        }

        public ScriptSecurityException(string message) : base(message)
        {
        }

        public ScriptSecurityException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
