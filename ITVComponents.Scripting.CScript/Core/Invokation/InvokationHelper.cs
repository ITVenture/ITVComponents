using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Scripting.CScript.Core.Methods;

namespace ITVComponents.Scripting.CScript.Core.Invokation
{
    public class InvokationHelper
    {
        /// <summary>
        /// the Delegate which is used to be invoked
        /// </summary>
        private MethodHelper.MethodBuffer dlg;

        /// <summary>
        /// Default parameters being passed to the delegate on invokation
        /// </summary>
        private object[] defaultParameters;

        /// <summary>
        /// the target on which to invoke the target method
        /// </summary>
        private object target;

        /// <summary>
        /// Initializes a new instance of the InvokationHelper class
        /// </summary>
        /// <param name="dlg">the delegate that can be invoked from somewhere</param>
        /// <param name="target">the target on which to invoke the target method</param>
        /// <param name="defaultParameters">default arguments being passed to the delegate on execution</param>
        public InvokationHelper(MethodInfo dlg, object target, object[] defaultParameters)
        {
            this.dlg = new MethodHelper.MethodBuffer
            {
                MethodInfo = dlg,
                IsGeneric = dlg.ContainsGenericParameters,
                ArgumentsLength = dlg.GetParameters().Length,
                IsExtension = false
            };

            this.defaultParameters = defaultParameters;
            this.target = target;
        }

        /// <summary>
        /// Prevents a default instance of the InvokationHelper class from being created
        /// </summary>
        private InvokationHelper()
        {
        }

        /// <summary>
        /// Invokes the underlaying method with the provided defaults and given additional parameters
        /// </summary>
        /// <param name="additionalParameters">the additional parameters that are supported by the method</param>
        /// <returns>the result of the invokation of the method</returns>
        public object Invoke(object[] additionalParameters, out bool success)
        {
            success = false;
            object[] args = (from t in defaultParameters select (!(t is FixtureMapper))?t:((FixtureMapper) t).Value).ToArray() ;
            if (additionalParameters != null && additionalParameters.Length != 0)
            {
                List<object> l = new List<object>();
                l.AddRange(defaultParameters);
                l.AddRange(additionalParameters);
                args = l.ToArray();
            }

            object[] callArgs;
            var callMethod = MethodHelper.FindMethodFromArray(true, new[] {dlg}, MethodHelper.GetTypeArray(args, false), args, out callArgs);
            if (callMethod != null)
            {
                success = true;
                return callMethod.MethodInfo.Invoke(target, args);
            }

            return null;
        }
    }
}
