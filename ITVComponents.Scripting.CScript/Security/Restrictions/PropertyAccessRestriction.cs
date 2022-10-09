using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Security.Restrictions
{
    public class PropertyAccessRestriction:IScriptingRestriction
    {
        public PolicyMode PolicyMode { get; internal set; }

        public IScriptingRestriction Clone()
        {
            return new PropertyAccessRestriction
            {
                AccessMode=AccessMode,
                PolicyMode=PolicyMode,
                Property = Property
            };
        }

        public PropertyInfo Property { get; internal set; }
        public PropertyAccessMode AccessMode { get; internal set; }
    }

    [Flags]
    public enum PropertyAccessMode
    {
        None = 0,
        Read=1,
        Write=2,
        Any = Read | Write
    }
}
