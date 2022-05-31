using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.AssemblyResolving;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions.Helpers;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    public class WebPartManager
    {
        private readonly IConfiguration config;

        private Dictionary<string, Dictionary<string,object>> configurations = new();

        private List<MethodRef> mvcRegistrationMethods = new();

        private List<MethodRef> endpointRegistrationMethods = new();

        private List<MethodRef> serviceRegistrationMethods = new();

        private List<MethodRef> authSchemeRegistrationMethods = new();

        /// <summary>
        /// Initializes a new instance of the WebPartManager class
        /// </summary>
        /// <param name="config">the configuration for the current app</param>
        public WebPartManager(IConfiguration config)
        {
            this.config = config;
            var options = config.GetSection<WebPartOptions>("ITVenture:WebParts");
            var WebPartTypes = (from t in options.Assemblies
                select new {Types = AssemblyResolver.FindAssemblyByFileName(t.AssemblyName).GetTypes()
                    .Where(n => n.IsPublic && n.IsAbstract && n.IsSealed &&  Attribute.IsDefined(n, typeof(WebPartAttribute))),
                    OptionPath = t.DetailConfigPath,
                    OptionPaths = t.DetailConfigPaths
                }).SelectMany(n => n.Types.Select(o => new {Type=o, ConfigPath=n.OptionPath, ConfigPaths = n.OptionPaths})).ToArray();
            foreach (var t in WebPartTypes)
            {
                AnalyzeType(t.Type, t.ConfigPath, t.ConfigPaths);
            }

        }

        /// <summary>
        /// Sample declaration for a WebPartRegistration method. To not call this method directly, it will throw an exception!
        /// </summary>
        /// <param name="manager">the applicationpartmanager that is used to register web-parts</param>
        /// <param name="configuration">the configuration that was provided for this web-part</param>
        public void RegisterWebPart(ApplicationPartManager manager, object configuration)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided manager
        /// </summary>
        /// <param name="manager">the manager on which custom webparts can be registered</param>
        public void RegisterParts(ApplicationPartManager manager)
        {
            foreach (var t in mvcRegistrationMethods)
            {
                Dictionary<string,object> opt = null;
                if (!string.IsNullOrEmpty(t.ConfigurationName) &&
                    configurations.TryGetValue(t.ConfigurationName, out var tmo))
                {
                    opt = tmo;
                }

                try
                {
                    InvokeMethod(t.Method, new object[] { manager }, opt);
                    //t.Method.Invoke(null, new object[] { manager, opt });
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to register {t.Method.DeclaringType.AssemblyQualifiedName}: {ex.Message}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// Sample declaration for a WebPartRegistration method. To not call this method directly, it will throw an exception!
        /// </summary>
        /// <param name="manager">the applicationpartmanager that is used to register web-parts</param>
        /// <param name="configuration">the configuration that was provided for this web-part</param>
        public void RegisterEndpoints(IEndpointRouteBuilder manager, object configuration)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided manager
        /// </summary>
        /// <param name="builder">the endpointbuilder where custom endpoints can be injected</param>
        public void RegisterEndpoints(IEndpointRouteBuilder builder)
        {
            foreach (var t in endpointRegistrationMethods)
            {
                Dictionary<string, object> opt = null;
                if (!string.IsNullOrEmpty(t.ConfigurationName) &&
                    configurations.TryGetValue(t.ConfigurationName, out var tmo))
                {
                    opt = tmo;
                }

                try
                {
                    //t.Method.Invoke(null, new[] { builder, opt });

                    InvokeMethod(t.Method, new object[] { builder }, opt);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to register {t.Method.DeclaringType.AssemblyQualifiedName}: {ex.Message}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// Sample declaration for a WebPartRegistration method. To not call this method directly, it will throw an exception!
        /// </summary>
        /// <param name="services">the services collection in which the required services can be injected</param>
        /// <param name="configuration">the configuration that was provided for this web-part</param>
        public void RegisterServices(IServiceCollection services, object configuration)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided manager
        /// </summary>
        /// <param name="services">the service-collection where custom services can be injected</param>
        public void RegisterServices(IServiceCollection services)
        {
            foreach (var t in serviceRegistrationMethods)
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
                    InvokeMethod(t.Method, new object[] { services }, opt);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to register {t.Method.DeclaringType.AssemblyQualifiedName}: {ex.Message}", LogSeverity.Error);
                }
            }
        }

        /// <summary>
        /// Sample declaration for a WebPartRegistration method. To not call this method directly, it will throw an exception!
        /// </summary>
        /// <param name="auth">the Authentication-builder on which a custom Authentication method is being registered</param>
        /// <param name="configuration">the configuration that was provided for this web-part</param>
        public void RegisterAuthenticationSchemes(AuthenticationBuilder auth, object configuration)
        {
            throw new NotImplementedException("This is a sample method! Implement it in your WebPart-Initializer");
        }

        /// <summary>
        /// Registers all configured Web-Parts in the provided authentication-builder
        /// </summary>
        /// <param name="auth">the authentication builder on which to register the custom authentication types</param>
        public void RegisterAuthenticationSchemes(AuthenticationBuilder auth)
        {
            foreach (var t in authSchemeRegistrationMethods)
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
                    InvokeMethod(t.Method, new object[] { auth }, opt);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to register {t.Method.DeclaringType.AssemblyQualifiedName}: {ex.Message}", LogSeverity.Error);
                }
            }
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
                else if (attr is MvcRegistrationMethodAttribute && MethodMatches(method, ()=> RegisterWebPart(default,default)))

                {
                    mvcRegistrationMethods.Add(new MethodRef{ConfigurationName = configPath, Method = method});
                }
                else if (attr is EndpointRegistrationMethodAttribute &&
                         MethodMatches(method, () => RegisterEndpoints(default, default)))
                {
                    endpointRegistrationMethods.Add(new MethodRef { ConfigurationName = configPath, Method = method });
                }
                else if (attr is ServiceRegistrationMethodAttribute &&
                         MethodMatches(method, () => RegisterServices(default, default)))
                {
                    serviceRegistrationMethods.Add(new MethodRef { ConfigurationName = configPath, Method = method });
                }
                else if (attr is AuthenticationRegistrationMethodAttribute &&
                         MethodMatches(method, () => RegisterAuthenticationSchemes(default, default)))
                {
                    authSchemeRegistrationMethods.Add(new MethodRef { ConfigurationName = configPath, Method = method });
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
                if (Attribute.IsDefined(p, typeof(WebPartConfigAttribute)))
                {
                    var att = (WebPartConfigAttribute)Attribute.GetCustomAttribute(p, typeof(WebPartConfigAttribute));
                    name = att.ConfigurationKey;
                }

                if (options != null && options.TryGetValue(name, out var v))
                {
                    l.Add(v);
                }
                else
                {
                    l.Add(null);
                }
            }

            method.Invoke(null, l.ToArray());
        }

        /// <summary>
        /// Compares the method definitions of two methods
        /// </summary>
        /// <param name="info">the method that was found on a WebPartInitializer</param>
        /// <param name="target">the appropriate method that is going to invoke the provided method during the initialization of an app</param>
        /// <returns>a values indicating whether an mvc-registration method is compatible to the registration method found on a webpart-declaration</returns>
        private bool MethodMatches(MethodInfo info, Expression<Action> target)
        {
            var targetMethod = MethodHelper.GetMethodInfo(target);
            var srcParams = info.GetParameters();
            var trgParams = targetMethod.GetParameters();
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
                     (from t in srcParams.Select((p, i) => new { p, i })
                         where t.i >= trgParams.Length-1
                         select t.p).All(p => Attribute.IsDefined(p, typeof(WebPartConfigAttribute))))
            {
                return true;
            }

            return false;
        }
    }
}
