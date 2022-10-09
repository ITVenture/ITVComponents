using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling
{
    internal class TemplateHandlerFactory:ITemplateHandlerFactory, IDisposable
    {
        private readonly IServiceProvider services;
        private readonly IWebPluginHelper pluginProvider;
        private readonly IHttpContextAccessor httpContext;
        private ConcurrentDictionary<Type, object> bufferedServices = new ConcurrentDictionary<Type, object>();
        private ConcurrentDictionary<string, IPlugin> bufferedPlugins = new ConcurrentDictionary<string, IPlugin>();
        private IRequestCultureFeature cult = null;
        private PluginFactory factory;
        public TemplateHandlerFactory(IServiceProvider services, IWebPluginHelper pluginProvider, IHttpContextAccessor httpContext)
        {
            this.services = services;
            this.pluginProvider = pluginProvider;
            this.httpContext = httpContext;
        }

        private PluginFactory Factory => factory ??= pluginProvider.GetFactory();

        public async Task<IHtmlContent> RenderHandlerComponent(IViewComponentHelper viewComponent, string componentTypeExpression,
            Dictionary<string, string> componentArguments)
        {
            var dic = new Dictionary<string, object>
            {
                { "GetService", GetService },
                {"GetPlugin", GetPlugin},
                {"Translate", Translate}
            };
            using (var session = ExpressionParser.BeginRepl(dic, s => DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession)))
            {
                var type = ProcessScript<Type>(session, componentTypeExpression);
                var paramDic = BuildArguments(session, componentArguments);

                return await viewComponent.InvokeAsync(type, paramDic);
            }
        }

        private IDictionary<string,object> BuildArguments(IDisposable session, Dictionary<string, string> componentArguments)
        {
            var paramDic = new Dictionary<string, object>();
            foreach (var item in componentArguments)
            {
                paramDic.Add(item.Key, ProcessScript<object>(session, item.Value));
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
