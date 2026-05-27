using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TreeCustomerOnboarding;
using ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.OnboardingViews.Blazor;

[WebPart]
public static class WebPartInit
{
    [LoadWebPartConfig]
    public static object? LoadOptions(IConfiguration config, string key, string path)
    {
        if (key == "ContextSettings")
        {
            return config.GetSection<OnboardingViewOptions>(path);
        }

        return null;
    }

    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services,
        [WebPartConfig("ContextSettings")] OnboardingViewOptions? options,
        [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
    {
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly, partTypeLoadBehavior);

        if (options is { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
        {
            var dic = new Dictionary<string, object>();
            var contextType = (Type)ExpressionParser.Parse(contextTypeName, dic);

            // The host's DbContext picks the strategy: tree-typed contexts route through the
            // hierarchy extension, flat-typed ones through the regular one. Both checks tolerate
            // a context that doesn't implement either interface (it just registers no handler).
            if (typeof(IHierarchySecurityContextWithOnboarding).IsAssignableFrom(contextType))
            {
                var method = typeof(DependencyInjectionExtensions).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(DependencyInjectionExtensions.AddMudBlazorHierarchyOnboardingViews));
                method(services, partTypeLoadBehavior);
            }
            else if (typeof(ISecurityContextWithOnboarding).IsAssignableFrom(contextType))
            {
                var method = typeof(DependencyInjectionExtensions).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(DependencyInjectionExtensions.AddMudBlazorOnboardingViews));
                method(services, partTypeLoadBehavior);
            }
        }
    }
}
