using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Optimization.LazyExecutors
{
    internal class LazyOp:IExecutor
    {
        public ScriptingPolicy Policy { get; }
        private Func<object, object, bool, object> invoker;
        private bool typeSave;
        public LazyOp(Func<object, object, bool, object> invoker, bool typeSave, ScriptingPolicy policy)
        {
            Policy = policy;
            this.invoker = invoker;
            this.typeSave = typeSave;
        }

        #region Implementation of IExecutor

        public bool CanExecute(object value, ScriptValue[] arguments)
        {
            return value == null && arguments.Length == 2;
        }

        public object Invoke(object value, ScriptValue[] arguments)
        {
            return invoker(arguments[0].GetValue(null, Policy), arguments[1].GetValue(null, Policy), typeSave);
        }

        #endregion
    }
}
