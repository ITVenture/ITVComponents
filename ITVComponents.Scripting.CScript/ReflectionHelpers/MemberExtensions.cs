using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;
using ValueType = ITVComponents.Scripting.CScript.ScriptValues.ValueType;

namespace ITVComponents.Scripting.CScript.ReflectionHelpers
{
    public static class MemberExtensions
    {
        public static object GetValue(this object target, string memberName, ScriptingPolicy policy = null)
        {
            return MemberAccessHelper.GetMemberValue(target, memberName, null, ValueType.PropertyOrField,
                policy ?? ScriptingPolicy.Default, MemberAccessMode.Read);
        }

        public static void SetValue(this object target, string memberName, object value, ScriptingPolicy  policy = null)
        {
            MemberAccessHelper.SetMemberValue(target, memberName, value, null, ValueType.PropertyOrField, policy??ScriptingPolicy.Default);
        }

        public static object GetValueType(this object target, string memberName)
        {
            return MemberAccessHelper.GetMemberType(target, memberName, null, ValueType.PropertyOrField);
        }
    }
}
