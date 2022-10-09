using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Security.Restrictions
{
    public class TypeAccessRestriction:IScriptingRestriction
    {
        public TypeAccessMode AccessMode { get; internal set; }
        public PolicyMode PolicyMode { get; internal set; }
        public IScriptingRestriction Clone()
        {
            return new TypeAccessRestriction
            {
                PolicyMode = PolicyMode,
                AccessMode = AccessMode,
                Type = Type
            };
        }

        public Type Type { get; internal set; }
    }

    [Flags]
    public enum TypeAccessMode
    {
        None=0,
        Direct = 1,
        InstanceMethod = 2,
        InstanceMember = 4,
        StaticMethod = 8,
        StaticMember= 16,
        Construct=32,
        Extension = 64,
        InstanceEvent=128,
        StaticEvent=256,
        Any=Direct|InstanceMethod|InstanceMember|StaticMethod|StaticMember|Construct|Extension|InstanceEvent|StaticEvent
    }
}
