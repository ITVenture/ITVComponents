using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Optimization
{
    public interface IScriptSymbol
    {
        void SetPreferredExecutor(IExecutor executor);

        object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success);

        bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck);
    }
}
