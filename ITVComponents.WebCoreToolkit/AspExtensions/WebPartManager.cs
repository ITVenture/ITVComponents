using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.AssemblyResolving;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions.Helpers;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    public class WebPartManager
    {
        private readonly IConfiguration config;

        private Dictionary<string, Dictionary<string,object>> configurations = new();

        private List<MethodRef> endpointRegistrationMethods = new();

        private List<MethodRef> metaExposalMethods = new();

        private ConcurrentDictionary<Type, List<MethodRef>> customMethods = new();

        private bool endPointsRegistrationDone = false;

        private ISharedObjHeap sharedHeap = new SharedObjectHeap();

        private MethodInfo customConfigMeth =
            typeof(WebPartManager).GetMethod("PartCustomObjectConfig", BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Initializes a new instance of the WebPartManager class
        /// </summary>
        /// <param name="config">the configuration for the current app</param>
        public WebPartManager(IConfiguration config)
        {
            this.config = config;
            var options = config.GetSection<WebPartOptions>("ITVenture:WebParts");
            var webPartTypes = (from t in options.Assemblies
                select new {Types = GetAssembly(t.AssemblyName).GetTypes()
                    .Where(n => n.IsPublic && n.IsAbstract && n.IsSealed &&  Attribute.IsDefined(n, typeof(WebPartAttribute))),
                    OptionPath = t.DetailConfigPath,
                    OptionPaths = t.DetailConfigPaths
                }).SelectMany(n => n.Types.Select(o => new {Type=o, ConfigPath=n.OptionPath, ConfigPaths = n.OptionPaths})).ToArray();
            foreach (var t in webPartTypes)
            {
                AnalyzeType(t.Type, t.ConfigPath, t.ConfigPaths);
            }

        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided manager
        /// </summary>
        /// <param name="manager">the manager on which custom webparts can be registered</param>
        public void RegisterParts(ApplicationPartManager manager)
        {
            CustomObjectConfig(manager);
        }

        /// <summary>
        /// Sample declaration for a WebPartRegistration method. To not call this method directly, it will throw an exception!
        /// </summary>
        /// <param name="manager">the applicationpartmanager that is used to register web-parts</param>
        /// <param name="endPointRegistry">the EndPoint registry that is used for post-processing the generated endpoints</param>
        /// <param name="configuration">the configuration that was provided for this web-part</param>
        public void RegisterPartEndpoints(WebApplication manager)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided manager
        /// </summary>
        /// <param name="builder">the endpointbuilder where custom endpoints can be injected</param>
        public void RegisterEndpoints(WebApplication builder)
        {
            try
            {
                InvokeMethods(endpointRegistrationMethods, new object[] { builder });
            }
            finally
            {
                endPointsRegistrationDone = true;
            }
        }

        /// <summary>
        /// Sample declaration for a WebPartRegistration method. To not call this method directly, it will throw an exception!
        /// </summary>
        /// <param name="manager">the applicationpartmanager that is used to register web-parts</param>
        /// <param name="endPointRegistry">the EndPoint registry that is used for post-processing the generated endpoints</param>
        /// <param name="configuration">the configuration that was provided for this web-part</param>
        public void ExposePartsEndpointMetaData(WebApplication manager)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Calls registered methods that turn on and configure EndPoint Metadata-Exposal
        /// </summary>
        /// <param name="builder">the endpointbuilder where custom endpoints have been injected</param>
        public void ExposeEndpointMetaData(WebApplication builder)
        {
            if (!endPointsRegistrationDone)
            {
                throw new InvalidOperationException("Expose End-Points first!");
            }

            InvokeMethods(metaExposalMethods, new object[] { builder });
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided manager
        /// </summary>
        /// <param name="services">the service-collection where custom services can be injected</param>
        public void RegisterServices(IServiceCollection services)
        {
            CustomObjectConfig(services);
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided authentication-builder
        /// </summary>
        /// <param name="auth">the authentication builder on which to register the custom authentication types</param>
        public void RegisterAuthenticationSchemes(AuthenticationBuilder auth)
        {
            CustomObjectConfig(auth);
        }

        /// <summary>
        /// Registers health checks on the given HealthChecksBuilder object
        /// </summary>
        /// <param name="builder">the HealthCheckBuilder that is used to </param>
        public void RegisterHealthChecks(IHealthChecksBuilder builder)
        {
            CustomObjectConfig(builder);
        }

        public void PartCustomObjectConfig<T>(T target)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Invokes all custom Configurators for the given Type
        /// </summary>
        /// <typeparam name="T">the Type of which an instance is being configured with this call</typeparam>
        /// <param name="target">the target object that is being configured with the call</param>
        public void CustomObjectConfig<T>(T target)
        {
            InvokeMethods<T>(customMethods, new object[] { target });
        }

        /// <summary>
        /// Analyzes the given type and extracts all aspect-methods for the web-part-registration
        /// </summary>
        /// <param name="t">the type that is marked as web-part entrypoint</param>
        /// <param name="cfgPath">the plain configpath</param>
        /// <param name="cfgPaths">the config-paths dictionary that can contain named configurations that configure specific aspects of a web-part</param>
        private void AnalyzeType(Type t, string cfgPath, Dictionary<string,string> cfgPaths)
        {
            var configPath = Guid.NewGuid().ToString("N");
            var allMethods = t.GetMethods(true).Where(n => Attribute.IsDefined(n, typeof(WebPartRegistrationMethodAttribute)));
            foreach (var method in allMethods)
            {
                var attr = Attribute.GetCustomAttribute(method, typeof(WebPartRegistrationMethodAttribute));
                if (attr is LoadWebPartConfigAttribute)
                {
                    var dic = new Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(cfgPath))
                    {
                        dic.Add("DEFAULT", method.Invoke(null,
                                        BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod,
                                        null,
                                        new object[] { config, cfgPath }, null));
                    }

                    if (cfgPaths != null && cfgPaths.Count != 0)
                    {
                        foreach (var tmp in cfgPaths)
                        {
                            dic.Add(tmp.Key, method.Invoke(null,
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod,
                                null,
                                new object[] { config, tmp.Key, tmp.Value }, null));
                        }
                    }

                    configurations.TryAdd(configPath, dic);
                }
                else if (attr is EndpointRegistrationMethodAttribute &&
                         MethodMatches(method, () => RegisterPartEndpoints(default)))
                {
                    endpointRegistrationMethods.Add(new MethodRef { ConfigurationName = configPath, Method = method });
                }
                else if (attr is EndpointMetaExposeAttribute &&
                         MethodMatches(method, () => ExposePartsEndpointMetaData(default)))
                {
                    metaExposalMethods.Add(new MethodRef { ConfigurationName = configPath, Method = method });
                }
                else if (attr is CustomConfiguratorAttribute cca &&
                         MethodMatches(method, MakeCustomConfigCall(cca.ConfiguredObjectType)))
                {
                    RegisterCustomMethod(cca.ConfiguredObjectType, new MethodRef { ConfigurationName = configPath, Method = method });
                }
            }
        }

        private void RegisterCustomMethod<T>(MethodRef method)
        {
            RegisterCustomMethod(typeof(T), method);
        }
        private void RegisterCustomMethod(Type configuredType, MethodRef method)
        {
            var l = customMethods.GetOrAdd(configuredType, _ => new List<MethodRef>());
            lock (l)
            {
                l.Add(method);
            }
        }

        private Expression<Action> MakeCustomConfigCall(Type configuredObjectType)
        {
            object t = null;
            if (configuredObjectType.IsValueType)
            {
                t = Activator.CreateInstance(configuredObjectType);
            }

            var ct = Expression.Constant(t, configuredObjectType);
            var gm = customConfigMeth.MakeGenericMethod(configuredObjectType);
            var me = Expression.Constant(this);
            var methCall = Expression.Call(me,gm,ct);
            return Expression.Lambda<Action>(methCall);
        }

        private void InvokeMethods<T>(ConcurrentDictionary<Type, List<MethodRef>> methodDic, object[] defaults)
        {
            if (methodDic.TryGetValue(typeof(T), out var methods))
            {
                lock (methods)
                {
                    foreach (var t in methods)
                    {
                        Dictionary<string, object> opt = null;
                        if (!string.IsNullOrEmpty(t.ConfigurationName) &&
                            configurations.TryGetValue(t.ConfigurationName, out var tmo))
                        {
                            opt = tmo;
                        }

                        try
                        {
                            //t.Method.Invoke(null, new[] { services, opt });
                            InvokeMethod(t.Method, defaults, opt);
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogEvent(
                                $"Failed to register {t.Method.DeclaringType.AssemblyQualifiedName}: {ex.Message}",
                                LogSeverity.Error);
                        }
                    }
                }
            }
        }

        private void InvokeMethods(List<MethodRef> methods, object[] defaults)
        {
            foreach (var t in methods)
            {
                Dictionary<string, object> opt = null;
                if (!string.IsNullOrEmpty(t.ConfigurationName) &&
                    configurations.TryGetValue(t.ConfigurationName, out var tmo))
                {
                    opt = tmo;
                }

                try
                {
                    //t.Method.Invoke(null, new[] { services, opt });
                    InvokeMethod(t.Method, defaults, opt);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to register {t.Method.DeclaringType.AssemblyQualifiedName}: {ex.Message}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// Invokes a method with the appropriate arguments
        /// </summary>
        /// <param name="method">the method to call for the registration of a specific aspect of a webpart</param>
        /// <param name="defaultArguments">the default arguments for the method</param>
        /// <param name="options">the options that contain optional arguments for the target-method</param>
        private void InvokeMethod(MethodInfo method, object[] defaultArguments, Dictionary<string, object> options)
        {
            var args = method.GetParameters();
            var l = new List<object>(defaultArguments);
            for (int i = defaultArguments.Length; i < args.Length; i++)
            {
                var p = args[i];
                string name = "DEFAULT";
                bool isGlobalObj = false;
                if (Attribute.IsDefined(p, typeof(WebPartConfigAttribute)))
                {
                    var att = (WebPartConfigAttribute)Attribute.GetCustomAttribute(p, typeof(WebPartConfigAttribute));
                    if (!string.IsNullOrEmpty(att.ConfigurationKey))
                    {
                        name = att.ConfigurationKey;
                    }

                    isGlobalObj = att is SharedObjectHeapAttribute;
                }

                if (!isGlobalObj)
                {
                    if (options != null && options.TryGetValue(name, out var v))
                    {
                        l.Add(v);
                    }
                    else
                    {
                        l.Add(null);
                    }
                }
                else
                {
                    l.Add(sharedHeap);
                }
            }

            method.Invoke(null, l.ToArray());
        }

        private Assembly GetAssembly(string name)
        {
            var retVal = AssemblyResolver.FindAssemblyByName(name);
            if (retVal == null)
            {
                throw new InvalidOperationException($"Assembly {name} not found.");
            }

            return retVal;
        }

        /// <summary>
        /// Compares the method definitions of two methods
        /// </summary>
        /// <param name="info">the method that was found on a WebPartInitializer</param>
        /// <param name="target">the appropriate method that is going to invoke the provided method during the initialization of an app</param>
        /// <returns>a values indicating whether an mvc-registration method is compatible to the registration method found on a webpart-declaration</returns>
        private bool MethodMatches(MethodInfo info, Expression<Action> target)
        {
            var targetMethod = LambdaHelper.GetMethodInfo(target);
            var srcParams = info.GetParameters();
            var trgParams = targetMethod.GetParameters();
            var extParams = (from t in srcParams.Select((p, i) => new { p, i })
                where t.i >= trgParams.Length
                select t.p).ToArray();
            if (trgParams.Length == srcParams.Length && (from s in srcParams.Select((p, i) => new { p, i })
                    join t in trgParams.Select((p, i) => new { p, i }) on s.i equals t.i
                                                         where s.p.ParameterType == t.p.ParameterType || t.p.ParameterType.IsAssignableFrom(s.p.ParameterType)
                    select new { s, t }).Count() == srcParams.Length)
            {
                return true;
            }
            else if (srcParams.Length > trgParams.Length && (from s in srcParams.Select((p, i) => new { p, i })
                         join t in trgParams.Select((p, i) => new { p, i }) on s.i equals t.i
                         where s.p.ParameterType == t.p.ParameterType ||
                               t.p.ParameterType.IsAssignableFrom(s.p.ParameterType)
                         select new { s, t }).Count() == trgParams.Length &&
                     (extParams.Length == 1 || extParams.All(p => Attribute.IsDefined(p, typeof(WebPartConfigAttribute)))))
            {
                return true;
            }

            return false;
        }
    }
}
