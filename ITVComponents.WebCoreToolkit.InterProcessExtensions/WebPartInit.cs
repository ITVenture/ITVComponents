using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Extensions;
using ITVComponents.WebCoreToolkit.InterProcessExtensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static InjectableProxyOptions LoadOptions(IConfiguration config, string path)
        {
            var retVal = config.GetSection<InjectableProxyOptions>(path);
            config.RefResolve(retVal);
            return retVal;
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, InjectableProxyOptions options)
        {
            if (options.Injectors.Count != 0)
            {
                services.UseInjectableProxies(o =>
                {
                    foreach (var proxy in options.Injectors)
                    {
                        var dic = new Dictionary<string, object>();
                        var tp = (Type)ExpressionParser.Parse(proxy.TypeExpression, dic);
                        o.ConfigureProxy(tp, c =>
                        {
                            c.ProxyName = proxy.ProxyName;
                            c.ObjectPatterns = proxy.ObjectPatterns.ToArray();
                        });
                    }
                });
            }
        }
    }
}
