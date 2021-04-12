using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
//using System.Runtime.Remoting.Contexts;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.Literals;
#if !Community
using ITVComponents.Helpers;
#endif
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting;
#if !Community
using ITVComponents.Logging;
#endif
using Microsoft.CSharp;

namespace ITVComponents.Scripting.CScript.Core.Native
{
    public static class NativeScriptHelper
    {
        private static readonly ConcurrentDictionary<string, NativeConfiguration> configurations = new ConcurrentDictionary<string, NativeConfiguration>();
        private static readonly ConcurrentDictionary<string, Lazy<MethodInfo>> scripts = new ConcurrentDictionary<string, Lazy<MethodInfo>>();
        private static readonly ConcurrentDictionary<string, Lazy<ScriptRunner<object>>> roslynScripts = new ConcurrentDictionary<string, Lazy<ScriptRunner<object>>>();

        /// <summary>
        /// Adds a reference to a specific Assembly. You can either speify the assembly with path, Name (i.e. System.dll) or with its AssemblyName 
        /// </summary>
        /// <param name="configuration">the configuration to use for adding the specified assembly-reference</param>
        /// <param name="reference">the reference identifying the required assembly</param>
        public static void AddReference(string configuration, string reference)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            lock (cfg)
            {
                if (reference == "--ROSLYN--")
                {
                    cfg.UseRoslyn = true;
                }
                else
                {
                    if (!cfg.References.Contains(reference, StringComparer.OrdinalIgnoreCase))
                    {
                        cfg.References.Add(reference);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a using to the list of usings for the given configuration
        /// </summary>
        /// <param name="configuration">the configuration that will use the given using-statement</param>
        /// <param name="usingParam">the using-statement that is required by linq-statements of this configuration</param>
        public static void AddUsing(string configuration, string usingParam)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            lock (cfg)
            {
                if (usingParam.StartsWith("using "))
                {
                    usingParam = usingParam.Substring(6);
                }

                if (usingParam.EndsWith(";"))
                {
                    usingParam = usingParam.Substring(0, usingParam.Length - 1);
                }

                if (!cfg.Usings.Contains(usingParam, StringComparer.OrdinalIgnoreCase))
                {
                    cfg.Usings.Add(usingParam);
                }
            }
        }



        /// <summary>
        /// Runs and compiles a linq-query
        /// </summary>
        /// <param name="configuration">the configuration that is used for compiling the provided linq-query</param>
        /// <param name="expression">the expression to run on the target object</param>
        /// <param name="arguments">arguments for the query. each property of the given object will lead to a parameter of the resulting method</param>
        /// <returns>the result of the compiled and executed method</returns>
        public static object RunLinqQuery(string configuration, string expression, IDictionary<string, object> arguments)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            if (cfg.UseRoslyn)
            {
                string roslynHash = GetFlatString(expression);
                var roslynScript = roslynScripts.GetOrAdd(roslynHash, new Lazy<ScriptRunner<object>>(() =>
                {
                    var scriptoptions = ScriptOptions.Default.WithImports(cfg.Usings.Union(new[] {"System", "System.Linq", "System.Collections.Generic"})).WithReferences(new[] {typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly, NamedAssemblyResolve.LoadAssembly("System.Linq"),NamedAssemblyResolve.LoadAssembly("Microsoft.CSharp")}.Union(from t in cfg.References select NamedAssemblyResolve.LoadAssembly(t))).WithOptimizationLevel(OptimizationLevel.Release);
                    var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper));
                    retVal.Compile();
                    return retVal.CreateDelegate();
                }));
                var dic = new ExpandoObject();
                var idic = dic as IDictionary<string, object>;
                foreach (var argument in arguments)
                {
                    idic[argument.Key] = argument.Value;
                }

                return AsyncHelpers.RunSync(() => roslynScript.Value(new NativeScriptObjectHelper {Global = idic}));
            }

            string code = $@"using System;
using System.Linq;
using System.Collections.Generic;
{string.Join("\r\n", cfg.Usings.Select(n => $"using {n};"))}
                // cfg: {configuration}
                namespace ITVComponents.Scripting.CScript.Native
                {{                
                    public class NativeScriptEval
                    {{      
                        public static object evalExpression({string.Join(",", from t in arguments orderby t.Key select $"{GetFriendlyName(t.Value.GetType())} {t.Key}")})
                        {{
                            return {expression};
                        }}
                    }}

                }}";
            string hash = GetFlatString(code);
            bool success = true;
            Exception compileX = null;
            var script = scripts.GetOrAdd(hash, new Lazy<MethodInfo>(() =>
            {
#if !Community
                LogEnvironment.LogDebugEvent($"Creating new native Script-Part. Hash: {hash}", LogSeverity.Report);
#endif
                CSharpCodeProvider provider = new CSharpCodeProvider();
                CompilerParameters parameters = new CompilerParameters();
                parameters.ReferencedAssemblies.Add(@"System.dll");
                parameters.ReferencedAssemblies.Add(@"System.Core.dll");
                if (cfg.References.Count != 0)
                {
                    parameters.ReferencedAssemblies.AddRange(cfg.References.ToArray());
                }

                parameters.GenerateInMemory = true;
                parameters.GenerateExecutable = false;
                try
                {
                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);

                    if (results.Errors.HasErrors)
                    {
                        StringBuilder sb = new StringBuilder();

                        foreach (CompilerError error in results.Errors)
                        {
                            sb.AppendLine(String.Format("Error ({0}): {1}", error.ErrorNumber, error.ErrorText));
                        }

                        throw new InvalidOperationException(sb.ToString());
                    }

                    Type binaryFunction =
                        results.CompiledAssembly.GetType("ITVComponents.Scripting.CScript.Native.NativeScriptEval");
                    return binaryFunction.GetMethod("evalExpression");
                }
                catch (Exception ex)
                {
#if (!Community)
                    LogEnvironment.LogDebugEvent(ex.OutlineException(), LogSeverity.Error);
                    LogEnvironment.LogDebugEvent(code,LogSeverity.Error);
#endif
                    compileX = ex;

                    success = false;
                }

                return null;
            }));



            var function = script.Value;
            if (!success)
            {
                Lazy<MethodInfo> dummy;
                scripts.TryRemove(hash, out dummy);
#if (!Community)
                LogEnvironment.LogDebugEvent("Method-creation failed. Method removed from buffer.", LogSeverity.Error);
#endif
                throw new ScriptException("Linq query failed. See log for further details."
                    ,compileX
                );
            }

            List<object> arg = new List<object>();
            arg.AddRange(from t in arguments orderby t.Key select t.Value);
            var result = function.Invoke(null, arg.ToArray());
            return result;
        }

        /// <summary>
        /// Runs and compiles a linq-query
        /// </summary>
        /// <param name="configuration">the configuration that is used for compiling the provided linq-query</param>
        /// <param name="target">the target object on which to run the query</param>
        /// <param name="nameOfTarget">the name of the parameter holding the target-object</param>
        /// <param name="expression">the expression to run on the target object</param>
        /// <param name="arguments">arguments for the query. each property of the given object will lead to a parameter of the resulting method</param>
        /// <returns>the result of the compiled and executed method</returns>
        public static object RunLinqQuery(string configuration, object target, string nameOfTarget, string expression, IDictionary<string, object> arguments)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            if (cfg.UseRoslyn)
            {
                string roslynHash = GetFlatString(expression);
                var roslynScript = roslynScripts.GetOrAdd(roslynHash, new Lazy<ScriptRunner<object>>(() =>
                {
                    var scriptoptions = ScriptOptions.Default.WithImports(cfg.Usings.Union(new[] {"System", "System.Linq", "System.Collections.Generic"})).WithReferences(new[] {typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly, NamedAssemblyResolve.LoadAssembly("System.Linq"),NamedAssemblyResolve.LoadAssembly("Microsoft.CSharp")}.Union(from t in cfg.References select NamedAssemblyResolve.LoadAssembly(t))).WithOptimizationLevel(OptimizationLevel.Release);
                    var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper));
                    retVal.Compile();
                    return retVal.CreateDelegate();
                }));
                var dic = new ExpandoObject();
                var idic = dic as IDictionary<string, object>;
                idic[nameOfTarget] = target;
                foreach (var argument in arguments)
                {
                    idic[argument.Key] = argument.Value;
                }

                return AsyncHelpers.RunSync(() => roslynScript.Value(new NativeScriptObjectHelper {Global = idic}));
            }

            string code = $@"using System;
using System.Linq;
using System.Collections.Generic;
{string.Join("\r\n", cfg.Usings.Select(n => $"using {n};"))}
                // cfg: {configuration}
                namespace ITVComponents.Scripting.CScript.Native
                {{                
                    public class NativeScriptEval
                    {{      
                        public static object evalExpression({GetFriendlyName(target.GetType())} {nameOfTarget}{(arguments.Keys.Count != 0 ? "," + string.Join(",", from t in arguments orderby t.Key select $"{GetFriendlyName(t.Value.GetType())} {t.Key}") : "")})
                        {{
                            return {expression};
                        }}
                    }}

                }}";
            string hash = GetFlatString(code);
            bool success = true;
            Exception compileX = null;
            var script = scripts.GetOrAdd(hash, new Lazy<MethodInfo>(() =>
            {
#if !Community
                LogEnvironment.LogDebugEvent($"Creating new native Script-Part. Hash: {hash}", LogSeverity.Report);
#endif
                CSharpCodeProvider provider = new CSharpCodeProvider();
                CompilerParameters parameters = new CompilerParameters();
                parameters.ReferencedAssemblies.Add(@"System.dll");
                parameters.ReferencedAssemblies.Add(@"System.Core.dll");
                if (cfg.References.Count != 0)
                {
                    parameters.ReferencedAssemblies.AddRange(cfg.References.ToArray());
                }

                parameters.GenerateInMemory = true;
                parameters.GenerateExecutable = false;
                try
                {
                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);

                    if (results.Errors.HasErrors)
                    {
                        StringBuilder sb = new StringBuilder();

                        foreach (CompilerError error in results.Errors)
                        {
                            sb.AppendLine(String.Format("Error ({0}): {1}", error.ErrorNumber, error.ErrorText));
                        }

                        throw new InvalidOperationException(sb.ToString());
                    }

                    Type binaryFunction =
                        results.CompiledAssembly.GetType("ITVComponents.Scripting.CScript.Native.NativeScriptEval");
                    return binaryFunction.GetMethod("evalExpression");
                }
                catch (Exception ex)
                {
#if (!Community)
                    LogEnvironment.LogDebugEvent(ex.OutlineException(), LogSeverity.Error);
                    LogEnvironment.LogDebugEvent(code,LogSeverity.Error);
#endif
                    compileX = ex;

                    success = false;
                }

                return null;
            }));



            var function = script.Value;
            if (!success)
            {
                Lazy<MethodInfo> dummy;
                scripts.TryRemove(hash, out dummy);
#if (!Community)
                LogEnvironment.LogDebugEvent("Method-creation failed. Method removed from buffer.", LogSeverity.Error);
#endif
                throw new ScriptException("Linq query failed. See log for further details."
                    ,compileX
                );
            }

            List<object> arg = new List<object>();
            arg.Add(target);
            arg.AddRange(from t in arguments orderby t.Key select t.Value);
            var result = function.Invoke(null, arg.ToArray());
            return result;
        }

        /// <summary>
        /// Creates a hash of the given method-text
        /// </summary>
        /// <param name="text">the generated method code</param>
        /// <returns>a unique hash-code (sha256) identifying the generated method uniquely</returns>
        private static string GetFlatString(string text)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            using (SHA256Managed hahsher = new SHA256Managed())
            {
                byte[] hash = hahsher.ComputeHash(bytes);
                string hashString = string.Empty;
                foreach (byte x in hash)
                {
                    hashString += String.Format("{0:x2}", x);
                }

                return hashString;
            }
        }

        /// <summary>
        /// Gets the friendly name of any type that can be used as parameter of a method
        /// </summary>
        /// <param name="type">the runtime type that is provided from the scripting</param>
        /// <returns>the friendly name that is accepted for compilation</returns>
        public static string GetFriendlyName(this Type type)
        {
            if (type == typeof(int))
                return "int";
            else if (type == typeof(short))
                return "short";
            else if (type == typeof(byte))
                return "byte";
            else if (type == typeof(bool))
                return "bool";
            else if (type == typeof(long))
                return "long";
            else if (type == typeof(float))
                return "float";
            else if (type == typeof(double))
                return "double";
            else if (type == typeof(decimal))
                return "decimal";
            else if (type == typeof(string))
                return "string";
            else if (type == typeof(ObjectLiteral))
            {
                return "dynamic";
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() != typeof(Nullable<>))
                return type.Name.Split('`')[0] + "<" + string.Join(", ", type.GetGenericArguments().Select(GetFriendlyName).ToArray()) + ">";
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return GetFriendlyName(type.GetGenericArguments().First()) + "?";
            else
            {
                if (type.Assembly.IsDynamic)
                {
                    return GetFriendlyName(type.BaseType);
                }

                return type.Name;
            }
        }
    }

    public class NativeScriptObjectHelper
    {
        public dynamic Global { get; set; }
    }
}
