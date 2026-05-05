using System.Reflection;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor;

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

            var userViewMethod = typeof(DependencyInjectionExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(DependencyInjectionExtensions.AddMudBlazorTreeTenantSecurityUserView)
                            && m.IsGenericMethodDefinition);
            userViewMethod.MakeGenericMethod(contextType).Invoke(null, new object[] { services });

            var tsvMethod = typeof(DependencyInjectionExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(DependencyInjectionExtensions.AddMudBlazorTreeTenantSecurityViews)
                            && m.IsGenericMethodDefinition);
            tsvMethod.MakeGenericMethod(contextType).Invoke(null, new object[] { services });
        }
    }
}
