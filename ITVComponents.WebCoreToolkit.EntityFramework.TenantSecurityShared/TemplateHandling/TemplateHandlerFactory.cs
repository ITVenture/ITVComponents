using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling
{
    internal class TemplateHandlerFactory : ITemplateHandlerFactory, IDisposable
    {
        private readonly IServiceProvider services;
        private readonly IWebPluginHelper pluginProvider;
        private readonly IHttpContextAccessor httpContext;
        private readonly ICoreSystemContext sysContext;
        private ConcurrentDictionary<Type, object> bufferedServices = new ConcurrentDictionary<Type, object>();
        private ConcurrentDictionary<string, IPlugin> bufferedPlugins = new ConcurrentDictionary<string, IPlugin>();

        private ConcurrentDictionary<Type, object> bufferedHandlers =
            new ConcurrentDictionary<Type, object>();
        private IRequestCultureFeature cult = null;
        private PluginFactory factory;

        public TemplateHandlerFactory(IServiceProvider services, IWebPluginHelper pluginProvider,
            IHttpContextAccessor httpContext, ICoreSystemContext sysContext)
        {
            this.services = services;
            this.pluginProvider = pluginProvider;
            this.httpContext = httpContext;
            this.sysContext = sysContext;
        }

        private PluginFactory Factory => factory ??= pluginProvider.GetFactory();

        public object GetBackEndHandler(TemplateModuleConfigurator configurator, out IDictionary<string,object> arguments)
        {
            using (var session = SetupScripting(out var sc))
            {
                var type = ProcessScript<Type>(session, configurator.ConfiguratorTypeBack);
                if (type.IsGenericTypeDefinition)
                {
                    type = sysContext.GetType().FinalizeType(type);
                }

                arguments = BuildArguments(session, configurator.ViewComponentParameters, sc);

                return bufferedHandlers.GetOrAdd(type, BuildHandler);
            }
        }

        private IDisposable SetupScripting(out IScope sc)
        {
            var dic = new Dictionary<string, object>
            {
                { "GetService", GetService },
                { "GetPlugin", GetPlugin },
                { "Translate", Translate },
                { "CurrentArg", null },
                { "Types", new Dictionary<string, object>() }
            };
            IScope scope = null;
            var retVal = ExpressionParser.BeginRepl(dic, s =>
            {
                scope = s.Scope;
                DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession);
            });

            sc = scope;
            return retVal;
        }

        private object BuildHandler(Type handlerType)
        {
            var constructors = handlerType.GetConstructors();
            var ct = (from t in constructors
                orderby t.GetParameters().Length
                select t
                into o
                where o.GetParameters().All(p => bufferedServices.GetOrAdd(p.ParameterType, y => services.GetService(y)) != null)
                select o).First();
            var pa = (from t in ct.GetParameters() select bufferedServices[t.ParameterType]).ToArray();
            return ct.Invoke(pa);
        }

        private IDictionary<string, object> BuildArguments(IDisposable session,
            ICollection<TemplateModuleConfiguratorParameter> componentArguments, IScope scope)
        {
            var paramDic = new Dictionary<string, object>();
            foreach (var item in componentArguments)
            {
                scope["CurrentArg"] = item;
                paramDic.Add(item.ParameterName, ProcessScript<object>(session, item.ParameterValue));
            }

            return paramDic;
        }

        private T ProcessScript<T>(IDisposable session, string targetExpression)
        {
            if (!string.IsNullOrEmpty(targetExpression))
            {
                if (!targetExpression.StartsWith("##"))
                {
                    return (T)ExpressionParser.Parse(targetExpression, session);
                }

                return (T)ExpressionParser.ParseBlock(targetExpression.Substring(2), session);
            }

            return default;
        }

        private object GetPlugin(string pluginName)
        {
            return bufferedPlugins.GetOrAdd(pluginName, s => Factory[s, true]);
        }

        private object GetService(Type serviceType)
        {
            return bufferedServices.GetOrAdd(serviceType, t => services.GetService(t));
        }

        private string Translate(string text)
        {
            cult ??= httpContext.HttpContext.Features.Get<IRequestCultureFeature>();
            string currentCulture = null;
            if (cult != null)
            {
                currentCulture = cult.RequestCulture.UICulture.Name;
            }

            return text.Translate(currentCulture);
        }

        public void Dispose()
        {
            bufferedServices.Clear();
            bufferedPlugins.Clear();
        }
    }
}
