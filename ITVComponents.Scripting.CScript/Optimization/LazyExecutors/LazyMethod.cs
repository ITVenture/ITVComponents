using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Optimization.LazyExecutors
{
    internal class LazyMethod:LazyInvoke
    {
        private readonly MethodInfo method;
        private readonly bool isStatic;
        private readonly bool isExtension;
        private readonly bool hasRef;
        internal LazyMethod(MethodInfo method, bool isStatic, bool isExtension, bool lastParams) : base((from t in method.GetParameters() select t.ParameterType).ToArray(), lastParams)
        {
            this.method = method;
            this.isStatic = isStatic;
            this.isExtension = isExtension;
            this.hasRef = method.GetParameters().Any(n => n.IsOut);
        }

        #region Overrides of LazyInvoke

        public override bool CanExecute(object value, ScriptValue[] arguments)
        {
            int diff = !isExtension ? 0 : 1;
            return ((((arguments[1] as SequenceValue)?.Sequence?.Length ?? 0) + diff) == types.Length) || lastParams;
        }

        public override object Invoke(object value, ScriptValue[] arguments)
        {
            ScriptValue[] args = ((SequenceValue) arguments[1]).Sequence;
            object[] raw = (from t in args select t.GetValue(null)).ToArray();
            if (isExtension)
            {
                raw = new[] {value}.Concat(raw).ToArray();
            }

            if (isStatic)
            {
                value = null;
            }

            object[] cargs = TranslateParams(raw);
            WritebackContainer[] writeBacks = null;
            object retVal;
            try
            {
                if (hasRef)
                {
                    writeBacks = MethodHelper.GetWritebacks(method, cargs, args);
                }

                retVal = method.Invoke(value, cargs);
            }
            finally
            {
                if (writeBacks != null)
                {
                    foreach (var container in writeBacks)
                    {
                        container.Target.SetValue(cargs[container.Index]);
                    }
                }
            }

            return retVal;
        }

        public override bool CanExecute(object value, object[] arguments)
        {
            int diff = !isExtension ? 0 : 1;
            return ((arguments.Length + diff) == types.Length) || lastParams;
        }

        public override object Invoke(object value, object[] arguments)
        {
            object[] raw = arguments;
            if (isExtension)
            {
                raw = new[] {value}.Concat(raw).ToArray();
            }

            if (isStatic)
            {
                value = null;
            }

            object[] cargs = TranslateParams(raw);
            WritebackContainer[] writeBacks = null;
            object retVal;
            try
            {
                if (hasRef)
                {
                    writeBacks = MethodHelper.GetWritebacks(method, cargs, arguments.Where(n => n is WritebackContainer).ToArray());
                }

                retVal = method.Invoke(value, cargs);
            }
            finally
            {
                if (writeBacks != null)
                {
                    foreach (var container in writeBacks)
                    {
                        container.Target.SetValue(cargs[container.Index]);
                    }
                }
            }

            return retVal;
        }

        #endregion
    }

    internal delegate object F(params object[] arguments);

    internal delegate object Fi(object i, params object[] arguments);
}
