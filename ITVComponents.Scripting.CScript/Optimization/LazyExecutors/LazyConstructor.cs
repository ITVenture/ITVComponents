using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Optimization.LazyExecutors
{
    internal class LazyConstructor:LazyInvoke
    {
        private ConstructorInfo constructor;

        public LazyConstructor(ConstructorInfo constructor, bool lastParams, ScriptingPolicy policy) : base((from c in constructor.GetParameters() select c.ParameterType).ToArray(), lastParams, policy) {
            this.constructor = constructor;
        }

        #region Overrides of LazyInvoke

        public override bool CanExecute(object value, ScriptValue[] arguments)
        {
            return (((arguments[1] as SequenceValue)?.Sequence?.Length ?? 0 ) == types.Length) || lastParams;
        }

        public override object Invoke(object value, ScriptValue[] arguments)
        {
            object[] args = TranslateParams((from a in arguments select a.GetValue(null, Policy)).ToArray());
            return constructor.Invoke(args);
        }

        #endregion
    }
}
