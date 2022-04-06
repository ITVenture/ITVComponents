using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.ExternalMethods;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.TypeConversion;

namespace ITVComponents.Scripting.CScript.Helpers
{
    public static class DefaultCallbacks
    {
        /// <summary>
        /// Initializes static members of the DefaultCallbacks class
        /// </summary>
        static DefaultCallbacks()
        {
            ExternalMethodHelper.RegisterClass(typeof(DefaultCallbacks));
        }

        /// <summary>
        /// Prepares the default callbacks provided by this type
        /// </summary>
        /// <param name="scope">the script scope that is used for expression or script execution</param>
        /// <param name="scriptingContext">the scripting context that is used for expression or script execution</param>
        public static void PrepareDefaultCallbacks(IScope scope, IDisposable scriptingContext)
        {
            ExternalMethodHelper.PrepareExpressionVariables(scope,
                new Dictionary<string, object>
                {
                    {"session", scriptingContext},
                    {"scope", scope},
                    {"variables", scope }
                });
        }

        /// <summary>
        /// Runs a script that is known by its path
        /// </summary>
        /// <param name="scriptingSession">the scripting session under which to execute the script</param>
        /// <param name="scriptName">the full path of the script</param>
        /// <returns>the result of the execution of the given script</returns>
        [ExternalMethod(MappedMethodName = "Run")]
        public static object RunScript([DefaultParameter(FixtureName = "session")]IDisposable scriptingSession, string scriptName)
        {
            ScriptFile<object> obj = ScriptFile<object>.FromFile(scriptName);
            return obj.Execute(scriptingSession);
        }

        /// <summary>
        /// Gets the reference to the current scope
        /// </summary>
        /// <param name="scope">the scope of the current scritp</param>
        /// <returns>the current scope</returns>
        [ExternalMethod(MappedMethodName = "Scope")]
        public static Scope Scope([DefaultParameter(FixtureName = "scope")] Scope scope)
        {
            return scope;
        }

        /// <summary>
        /// Runs a script that is known by its path
        /// </summary>
        /// <param name="scriptingSession">the scripting session under which to execute the script</param>
        /// <param name="script">the stream that contains the script</param>
        /// <returns>the result of the execution of the given script</returns>
        [ExternalMethod(MappedMethodName = "RunStream")]
        public static object RunFromStream([DefaultParameter(FixtureName = "session")]IDisposable scriptingSession, Stream script)
        {
            try
            {
                ScriptFile<object> obj = ScriptFile<object>.FromStream(script);
                return obj.Execute(scriptingSession);
            }
            finally
            {
                script.Dispose();
            }
        }

        /// <summary>
        /// Returns a Copy of the current active scope
        /// </summary>
        /// <param name="scope">the current variable scope</param>
        /// <returns>rooted scope based on the current variables and their values</returns>
        [ExternalMethod(MappedMethodName = "CopyScope")]
        public static IScope CopyScope([DefaultParameter(FixtureName = "scope")] Scope scope)
        {
            return new Scope(scope);
        }

        /// <summary>
        /// Returns a list representing the current active scope
        /// </summary>
        /// <param name="scope">the current variable scope</param>
        /// <returns>a list of all current variables with their values</returns>
        [ExternalMethod(MappedMethodName = "Var")]
        public static object PrintVariables([DefaultParameter(FixtureName = "scope")]Scope scope)
        {
            return scope.ToList();
        }

        /// <summary>
        /// Parses the given expression in the current scripting context
        /// </summary>
        /// <param name="scriptingSession">the active scripting session</param>
        /// <param name="expression">the expression to parse</param>
        /// <returns>the result of the expression</returns>
        [ExternalMethod(MappedMethodName = "Parse")]
        public static object Parse([DefaultParameter(FixtureName = "session")] IDisposable scriptingSession,
            string expression)
        {
            return ExpressionParser.Parse(expression, scriptingSession);
        }

        /// <summary>
        /// Parses the given expression in the current scripting context
        /// </summary>
        /// <param name="context">the context in which the execution must take place</param>
        /// <param name="expression">the expression to parse</param>
        /// <returns>the result of the expression</returns>
        [ExternalMethod(MappedMethodName="Eval")]
        public static object Eval(IScope context, string expression)
        {
            return ExpressionParser.Parse(expression,context);
        }

        /// <summary>
        /// Wraps the given object in a DictionaryWrapper
        /// </summary>
        /// <param name="obj">the object to be wrapped</param>
        /// <returns>a KeyValue provider that enables the script to handle and object like a dictionary</returns>
        [ExternalMethod(MappedMethodName="WrapKV")]
        public static IBasicKeyValueProvider WrapKV(IScope obj)
        {
            return new DictionaryWrapper(obj);
        }

        /// <summary>
        /// Converts an object from one type to an other
        /// </summary>
        /// <param name="value">the value to be converted</param>
        /// <param name="target">the target type</param>
        /// <returns>the converted value</returns>
        [ExternalMethod(MappedMethodName = "ChangeType")]
        public static object Convert(object value, Type target)
        {
            return TypeConverter.Convert(value, target);
        }
    }
}
