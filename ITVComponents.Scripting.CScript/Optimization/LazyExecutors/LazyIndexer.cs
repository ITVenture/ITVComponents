using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Optimization.LazyExecutors
{
    internal class LazyIndexer:LazyInvoke
    {
        private PropertyInfo indexProperty;

        public LazyIndexer(PropertyInfo indexProperty, bool lastParams, ScriptingPolicy policy):base((from t in indexProperty.GetIndexParameters() select t.ParameterType).ToArray(), lastParams, policy)
        {
            this.indexProperty = indexProperty;
        }

        #region Overrides of LazyInvoke

        public override bool CanExecute(object value, ScriptValue[] arguments)
        {
            return value != null && (arguments.Length == types.Length || lastParams);
        }

        public override object Invoke(object value, ScriptValue[] arguments)
        {
            return indexProperty.GetValue(value, (from t in arguments select t.GetValue(null,Policy)).ToArray());
        }

        #endregion
    }
}
