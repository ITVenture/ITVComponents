using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
//using System.Runtime.Remoting.Contexts;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.AssemblyResolving;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using System.DirectoryServices.AccountManagement;
using ITVComponents.Logging;
using Microsoft.CSharp;

namespace ITVComponents.Scripting.CScript.Core.Native
{
    public static class NativeScriptHelper
    {
        private static readonly ConcurrentDictionary<string, NativeConfiguration> configurations = new ConcurrentDictionary<string, NativeConfiguration>();
        //private static readonly ConcurrentDictionary<string, Lazy<ScriptRunner<object>>> roslynScripts = new ConcurrentDictionary<string, Lazy<ScriptRunner<object>>>();
        private static object initializationLock = new object();
        /*private static InteractiveAssemblyLoader loader;
        private static DateTime loaderInitializationTime;*/

        /// <summary>
        /// Adds a reference to a specific Assembly. You can either specify the assembly with path, Name (i.e. System.dll) or with its AssemblyName 
        /// </summary>
        /// <param name="configuration">the configuration to use for adding the specified assembly-reference</param>
        /// <param name="reference">the reference identifying the required assembly</param>
        public static void AddReference(string configuration, string reference)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            bool stubborn = false;
            if (reference.StartsWith("#S#"))
            {
                stubborn = true;
                reference = reference.Substring(3);
            }
            lock (cfg)
            {
                if (reference == "--ROSLYN--")
                {
                    LogEnvironment.LogEvent("Legacy Reference '--ROSLYN--' is ignored.", LogSeverity.Warning);
                }
                else if (reference == "--AUTOREF--")
                {
                    cfg.AutoReferences = true;
                }
                else if (reference == "--NOAUTOREF--")
                {
                    cfg.AutoReferences = false;
                }
                else
                {
                    cfg.AddReference(reference, stubborn);
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
            bool stubborn = false;
            if (usingParam.StartsWith("#S#"))
            {
                stubborn = true;
                usingParam = usingParam.Substring(3);
            }

            lock (cfg)
            {
                if (usingParam.StartsWith("using "))
                {
                    usingParam = usingParam.Substring(6).Trim();
                }

                if (usingParam.EndsWith(";"))
                {
                    usingParam = usingParam.Substring(0, usingParam.Length - 1).Trim();
                }

                cfg.AddUsing(usingParam, stubborn);
            }
        }

        /// <summary>
        /// Registers a type on the given configuration including usings when required
        /// </summary>
        /// <param name="configuration">the configuration native configuration name</param>
        /// <param name="usingType">the using-type on which to apply the references</param>
        /// <param name="includeUsings">indicates whether to make the type and all types of the namespace available implicitly</param>
        public static void AddAutoRef(string configuration, Type usingType, bool includeUsings)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            ApplyAutoRef(cfg, usingType, includeUsings);
        }

        /// <summary>
        /// Sets a value on the given configuration that indicates whether automatic referencing should be used for native script-parts
        /// </summary>
        /// <param name="configuration">the configuration that will be used in native scripts</param>
        /// <param name="enabled">a value indicating whether auto-referencing must be enabled</param>
        public static void SetAutoReferences(string configuration, bool enabled)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            lock (cfg)
            {
                cfg.AutoReferences = enabled;
            }
        }

        /// <summary>
        /// Resets a specific NativeConfiguration. If no name is provided, all known NativeConfigurations are reset.
        /// </summary>
        /// <param name="configuration">the configuration to reset</param>
        public static void ResetNativeScripts(string configuration = null)
        {
            if (string.IsNullOrEmpty(configuration))
            {
                var ks = configurations.Keys.ToArray();
                Array.ForEach(ks, ResetNativeScripts);
            }
            else
            {
                var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
                cfg.Reset(true);
            }
        }

        /// <summary>
        /// Runs and compiles a linq-query
        /// </summary>
        /// <param name="configuration">the configuration that is used for compiling the provided linq-query</param>
        /// <param name="expression">the expression to run on the target object</param>
        /// <param name="arguments">arguments for the query. each property of the given object will lead to a parameter of the resulting method</param>
        /// <param name="serviceProvider">optional service-provider the script may resolve services from through <c>Inject&lt;T&gt;()</c>. Pass a scope, not the root provider, or scoped services stay unreachable</param>
        /// <returns>the result of the compiled and executed method</returns>
        public static object RunLinqQuery(string configuration, string label, string expression, IDictionary<string, object> arguments, IServiceProvider serviceProvider = null)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            string roslynHash = GetFlatString(expression);
            var roslynScript = cfg.GetOrAddScript(label, roslynHash, new Lazy<ScriptRunner<object>>(() =>
            {
                ScriptOptions scriptoptions;
                lock (cfg)
                {
                    if (cfg.AutoReferences)
                    {
                        ApplyAutoRef(cfg, arguments.Values, false);
                    }

                    scriptoptions = ScriptOptions.Default
                        .WithImports(cfg.Usings.Union(new[] { "System", "System.Linq", "System.Collections.Generic" }))
                        .WithReferences(
                            new[]
                            {
                                typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly,
                                AssemblyResolver.FindAssemblyByName("System.Linq"),
                                AssemblyResolver.FindAssemblyByName("Microsoft.CSharp")
                            }.Union(from t in cfg.References select AssemblyResolver.FindAssemblyByName(t)).ToArray())
                        .WithOptimizationLevel(OptimizationLevel.Release);
                }
                //var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), Loader);
                var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), cfg.AssemblyLoader);
                retVal.Compile();
                return retVal.CreateDelegate();
            }));
            var dic = new ExpandoObject();
            var idic = dic as IDictionary<string, object>;
            foreach (var argument in arguments)
            {
                idic[argument.Key] = argument.Value;
            }

            return AsyncHelpers.RunSync(() => roslynScript.Value(new NativeScriptObjectHelper { Global = idic, ServiceProvider = serviceProvider }));
        }

        /// <summary>
        /// Runs and compiles a linq-query
        /// </summary>
        /// <param name="configuration">the configuration that is used for compiling the provided linq-query</param>
        /// <param name="target">the target object on which to run the query</param>
        /// <param name="nameOfTarget">the name of the parameter holding the target-object</param>
        /// <param name="expression">the expression to run on the target object</param>
        /// <param name="arguments">arguments for the query. each property of the given object will lead to a parameter of the resulting method</param>
        /// <param name="serviceProvider">optional service-provider the script may resolve services from through <c>Inject&lt;T&gt;()</c>. Pass a scope, not the root provider, or scoped services stay unreachable</param>
        /// <returns>the result of the compiled and executed method</returns>
        public static object RunLinqQuery(string configuration, object target, string nameOfTarget, string label, string expression, IDictionary<string, object> arguments, IServiceProvider serviceProvider = null)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            string roslynHash = GetFlatString(expression);
            var roslynScript = cfg.GetOrAddScript(label, roslynHash, new Lazy<ScriptRunner<object>>(() =>
            {
                ScriptOptions scriptoptions;
                lock (cfg)
                {
                    if (cfg.AutoReferences)
                    {
                        ApplyAutoRef(cfg, arguments.Values, false);
                        ApplyAutoRef(cfg, new[] { target }, true);
                    }

                    scriptoptions = ScriptOptions.Default
                        .WithImports(cfg.Usings.Union(new[] { "System", "System.Linq", "System.Collections.Generic" }))
                        .WithReferences(
                            new[]
                            {
                                typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly,
                                AssemblyResolver.FindAssemblyByName("System.Linq"),
                                AssemblyResolver.FindAssemblyByName("Microsoft.CSharp")
                            }.Union(from t in cfg.References select AssemblyResolver.FindAssemblyByName(t)).ToArray())
                        .WithOptimizationLevel(OptimizationLevel.Release);
                }

                //var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), Loader);
                var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), cfg.AssemblyLoader);
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

            return AsyncHelpers.RunSync(() => roslynScript.Value(new NativeScriptObjectHelper { Global = idic, ServiceProvider = serviceProvider }));
        }

        public static T RunCustomCode<T, TInput>(string configuration, TInput target, string label, string expression)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            string roslynHash = GetFlatString(expression);
            var roslynScript = cfg.GetOrAddScript($"#{label}", roslynHash, new Lazy<ScriptRunner<object>>(() =>
            {
                ScriptOptions scriptoptions;
                lock (cfg)
                {
                    if (cfg.AutoReferences)
                    {
                        ApplyAutoRef(cfg, new object[] { target }, true);
                    }

                    scriptoptions = ScriptOptions.Default
                        .WithImports(cfg.Usings.Union(new[] { "System", "System.Linq", "System.Collections.Generic" }))
                        .WithReferences(
                            new[]
                            {
                                typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly,
                                AssemblyResolver.FindAssemblyByName("System.Linq"),
                                AssemblyResolver.FindAssemblyByName("Microsoft.CSharp")
                            }.Union(from t in cfg.References select AssemblyResolver.FindAssemblyByName(t)).ToArray())
                        .WithOptimizationLevel(OptimizationLevel.Release);
                }

                //var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), Loader);
                var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), cfg.AssemblyLoader);
                retVal.Compile();
                return retVal.CreateDelegate();
            }));

            return (T)AsyncHelpers.RunSync(() => roslynScript.Value(target));
        }

        /// <summary>
        /// Compiles an expression from c# code
        /// </summary>
        /// <typeparam name="T">the expected expression-type</typeparam>
        /// <param name="configuration">the configuration name to use for the given expression</param>
        /// <param name="expression">the expression as string</param>
        /// <returns>an Expression object</returns>
        public static Expression<T> CompileExpression<T>(string configuration, string label, string expression)
        {
            var cfg = configurations.GetOrAdd(configuration, new NativeConfiguration());
            string roslynHash = GetFlatString(expression);
            var roslynScript = cfg.GetOrAddExpressionBuilder(label, roslynHash, new Lazy<LambdaHolder>(() =>
            {
                ScriptOptions scriptoptions;
                lock (cfg)
                {
                    if (cfg.AutoReferences)
                    {
                        ApplyAutoRef(cfg, typeof(T), true);
                    }

                    scriptoptions = ScriptOptions.Default
                        .WithImports(cfg.Usings.Union(new[] { "System", "System.Linq", "System.Collections.Generic" }))
                        .WithReferences(
                            new[]
                            {
                                typeof(FileStyleUriParser).Assembly, typeof(Action).Assembly,
                                AssemblyResolver.FindAssemblyByName("System.Linq"),
                                AssemblyResolver.FindAssemblyByName("Microsoft.CSharp")
                            }.Union(from t in cfg.References select AssemblyResolver.FindAssemblyByName(t)).ToArray())
                        .WithOptimizationLevel(OptimizationLevel.Release);
                }

                //var retVal = CSharpScript.Create(expression, scriptoptions, typeof(NativeScriptObjectHelper), Loader);
                var retVal = CSharpScript.Create<Expression<T>>(expression, scriptoptions,null, cfg.AssemblyLoader);
                retVal.Compile();
                return new LambdaHolder(retVal.CreateDelegate());
            }));

            return roslynScript.Value.Invoke<Expression<T>>();
        }

        /*private static InteractiveAssemblyLoader Loader
        {
            get
            {
                var retVal = loader;
                if (loader == null)
                {
                    lock (initializationLock)
                    {
                        if (loader == null)
                        {
                            loader = new InteractiveAssemblyLoader();
                            loaderInitializationTime = DateTime.Now;
                        }
                    }
                }
                /*else
                {
                    var diff = DateTime.Now.Subtract(loaderInitializationTime);
                    if (diff.TotalMinutes > 10)
                    {
                        lock (initializationLock)
                        {
                            diff = DateTime.Now.Subtract(loaderInitializationTime);
                            if (diff.TotalMinutes > 10)
                            {
                                var scripts = roslynScripts.ToArray();
                                foreach (var sc in scripts)
                                {
                                    if (sc.Value.IsValueCreated)
                                    {
                                        roslynScripts.TryRemove(sc.Key, out _);
                                    }
                                }
                            }

                            loader.Dispose();
                            loader = null;
                            retVal = Loader;
                        }
                    }
                }*/

                /*return retVal;
            }
        }*/

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
                StringBuilder hashString = new StringBuilder();
                foreach (byte x in hash)
                {
                    hashString.AppendFormat("{0:x2}", x);
                }

                return hashString.ToString();
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
                return $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyName).ToArray())}>";
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return $"{GetFriendlyName(type.GetGenericArguments().First())}?";
            else
            {
                if (type.Assembly.IsDynamic)
                {
                    return GetFriendlyName(type.BaseType);
                }

                return type.Name;
            }
        }

        /// <summary>
        /// Applies required references and usings to the given native-script configuration
        /// </summary>
        /// <param name="cfg">the script configuration on which to add required references</param>
        /// <param name="scriptObjects">the objects that will be passed to the script</param>
        /// <param name="usings">indicates whether to apply also the usings on the given configuration</param>
        private static void ApplyAutoRef(NativeConfiguration cfg, ICollection<object> scriptObjects, bool usings)
        {
            
            foreach (var obj in scriptObjects)
            {
                if (obj != null)
                {
                    
                    var contextType = obj.GetType();
                    ApplyAutoRef(cfg, contextType, usings);
                }
            }
        }

        /// <summary>
        /// Adds References and usings to the given native-script configuration, that are required to interact with a specific type
        /// </summary>
        /// <param name="cfg">the native configuration object</param>
        /// <param name="contextType">the context-type</param>
        /// <param name="usings">indicates whether to add usings as well</param>
        private static void ApplyAutoRef(NativeConfiguration cfg, Type contextType, bool usings)
        {
            var sysAssembly = typeof(object).Assembly.FullName;
            List<Type> allTheTypes = new List<Type>();
            while (contextType != null && contextType != typeof(object))
            {
                var ass = contextType.Assembly;

                if (!ass.IsDynamic)
                {
                    allTheTypes.Add(contextType);
                }

                var ifs = contextType.GetInterfaces();
                var bt = contextType.BaseType;
                allTheTypes.AddRange(ifs.Where(n => !n.Assembly.IsDynamic));
                contextType = bt;
            }

            //if (assemblyName != sysAssembly)
            foreach (var t in allTheTypes)
            {
                var nameSpace = t.Namespace;
                var assemblyName = t.Assembly.FullName;
                lock (cfg)
                {

                    if (assemblyName != sysAssembly && !cfg.References.Contains(assemblyName) && !string.IsNullOrEmpty(assemblyName))
                    {
                        cfg.AddReference(assemblyName);
                    }

                    if (usings && !cfg.Usings.Contains(nameSpace) && !string.IsNullOrEmpty(nameSpace))
                    {
                        cfg.AddUsing(nameSpace);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Globals-object of every roslyn-script compiled by the <see cref="NativeScriptHelper"/>. Roslyn puts each
    /// public member of this type directly into the script-scope — that is why a script writes <c>Global.Db</c>,
    /// and why <see cref="Inject{T}"/> is callable without any prefix.
    /// </summary>
    public class NativeScriptObjectHelper
    {
        /// <summary>Gets or sets the named values handed to the script (addressed as <c>Global.Xyz</c>).</summary>
        public dynamic Global { get; set; }

        /// <summary>
        /// Gets or sets the service-provider a script resolves services from. Set by whoever runs the script;
        /// null when the calling context has none.
        /// </summary>
        public IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// Resolves a service from the executing context, e.g.
        /// <c>Inject&lt;IGlobalSettings&lt;MyOptions&gt;&gt;()</c>. Throws when no provider was supplied or the
        /// service is not registered — a script silently continuing with null would fail further down, at a
        /// place that no longer names the service that was actually missing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The returned instance belongs to the surrounding scope. Never dispose it and never wrap it in a
        /// <c>using</c>: that would tear down a service (a DbContext, say) the caller still relies on. Note also
        /// that scoped services are only resolvable when the caller passed a scope rather than the root provider.
        /// </para>
        /// <para>
        /// <b>Do not inject a DbContext directly.</b> The scope handed in is the caller's own — under Blazor that
        /// is the circuit, so the context would be the very instance the running UI is working with, and a script
        /// enumerating while the page renders hits "a second operation was started on this context". Inject the
        /// <c>IDbContextFactory&lt;T&gt;</c> instead and open a context per operation. Services that already use
        /// the factory internally are unproblematic — the caveat is about pulling the context itself, and about
        /// services that still take one through their constructor.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the service-type to resolve</typeparam>
        /// <returns>the resolved service</returns>
        public T Inject<T>()
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException($"Unable to inject {typeof(T)}: the executing context did not provide a service-provider.");
            }

            if (ServiceProvider.GetService(typeof(T)) is not T retVal)
            {
                throw new InvalidOperationException($"Unable to inject {typeof(T)}: no such service is registered in the current scope.");
            }

            return retVal;
        }

        /// <summary>
        /// Same as <see cref="Inject{T}"/>, but yields the default-value instead of throwing when the service is
        /// unavailable. For optional dependencies a script can do without.
        /// </summary>
        /// <typeparam name="T">the service-type to resolve</typeparam>
        /// <returns>the resolved service, or the default-value of <typeparamref name="T"/></returns>
        public T InjectOrDefault<T>()
        {
            return ServiceProvider?.GetService(typeof(T)) is T retVal ? retVal : default;
        }
    }
}
