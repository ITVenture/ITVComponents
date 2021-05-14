using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Helpers;
using ValueType = ITVComponents.Scripting.CScript.Helpers.ValueType;

namespace ITVComponents.Scripting.CScript.ReflectionHelpers
{
    public static class MemberExtensions
    {
        public static object GetValue(this object target, string memberName)
        {
            return MemberAccessHelper.GetMemberValue(target, memberName, null, ValueType.PropertyOrField);
        }

        public static void SetValue(this object target, string memberName, object value)
        {
            MemberAccessHelper.SetMemberValue(target, memberName, value, null, ValueType.PropertyOrField);
        }
    }
}
