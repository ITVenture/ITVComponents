using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.ScriptValues
{
    internal static class ScriptValueHelper
    {
        public static T GetScriptValueResult<T>(ScriptValue value, bool alwaysReturn, ScriptingPolicy policy) 
        {
            if (value is Throw)
            {
                object error = value.GetValue(null, policy);
                
                if (error is ScriptException sci)
                {
                    throw sci;
                }
                
                if (error is Exception ex)
                {
                    throw new ScriptException("Error while executing Script", ex);
                }

                throw new ScriptException(error.ToString());
            }

            if ((value is IPassThroughValue && !(value is ReturnValue))||!(alwaysReturn || value is ReturnValue))
            {
                return default(T);
            }

            object tmp = value.GetValue(null, policy);
            ObjectLiteral olit = tmp as ObjectLiteral;
            Type t = typeof(T);
            if (olit != null && t.IsInterface)
            {
                tmp = olit.Cast(t);
            }
            return (T) tmp;
        }
    }
}
