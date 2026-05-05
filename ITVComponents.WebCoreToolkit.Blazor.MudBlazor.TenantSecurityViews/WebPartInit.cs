using System.Reflection;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor;

[WebPart]
public static class WebPartInit
{
    [LoadWebPartConfig]
    public static object? LoadOptions(IConfiguration config, string key, string path)
    {
        if (key == "ContextSettings")
        {
            return config.GetSection<SecurityContextOptions>(path);
        }

        return null;
    }

    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services,
        [WebPartConfig("ContextSettings")] SecurityContextOptions? options)
    {
        if (options is { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
        {
            var dic = new Dictionary<string, object>();
            var contextType = (Type)ExpressionParser.Parse(contextTypeName, dic);

            var openMethod = typeof(DependencyInjectionExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(DependencyInjectionExtensions.AddMudBlazorTenantSecurityViews)
                            && m.IsGenericMethodDefinition);
            openMethod.MakeGenericMethod(contextType).Invoke(null, new object[] { services });
        }
    }
}
