using System.Reflection;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTreeTenantSecurityUserView.Blazor;

[WebPart]
public static class WebPartInit
{
    // Mirrors the MVC AspNetCoreTreeTenantSecurityUserView WebPartInit: a single
    // DetailConfigPath in appsettings-parts.json drives this part — WebPartManager
    // then dispatches the 2-arg overload and stores the result under the "DEFAULT" key.
    [LoadWebPartConfig]
    public static object? LoadOptions(IConfiguration config, string path)
    {
        return config.GetSection<SecurityContextOptions>(path);
    }

    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services,
        [WebPartConfig] SecurityContextOptions? options,
        [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
    {
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly, partTypeLoadBehavior);

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
