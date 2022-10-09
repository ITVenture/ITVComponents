using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Optimization
{
    public interface IExecutor
    {
        ScriptingPolicy Policy { get; }

        bool CanExecute(object value, ScriptValue[] arguments);

        object Invoke(object value, ScriptValue[] arguments);
    }
}
