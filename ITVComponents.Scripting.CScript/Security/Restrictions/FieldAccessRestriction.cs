using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Security.Restrictions
{
    public class FieldAccessRestriction: IScriptingRestriction
    {
        public PolicyMode PolicyMode { get; internal set; }
        public IScriptingRestriction Clone()
        {
            return new FieldAccessRestriction
            {
                PolicyMode = PolicyMode,
                AccessMode = AccessMode,
                Field = Field
            };
        }

        public FieldInfo Field { get; internal set; }
        public FieldAccessMode AccessMode { get; internal set; }
    }

    [Flags]
    public enum FieldAccessMode
    {
        None = 0,
        Read = 1,
        Write = 2,
        Any = Read | Write
    }
}
