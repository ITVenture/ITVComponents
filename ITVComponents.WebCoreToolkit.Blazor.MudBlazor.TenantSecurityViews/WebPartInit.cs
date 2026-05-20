using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
        [WebPartConfig("ContextSettings")] SecurityContextOptions? options,
        [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
    {
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly, partTypeLoadBehavior);

        // Client-side assets emitted by the host's single <ITVentureReferences />.
        // BlazorMonaco powers the code editors (DiagnosticsQueries / Dashboard widgets / HealthScripts).
        services.AddToolkitClientScript("_content/BlazorMonaco/jsInterop.js");
        services.AddToolkitClientScript("_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js");
        services.AddToolkitClientScript("_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js");
        // WidgetRenderer click-action delegation (asset ships with the base MudBlazor library).
        services.AddToolkitClientScript("_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor/widget-actions.js");

        if (options is { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
        {
            var dic = new Dictionary<string, object>();
            var contextType = (Type)ExpressionParser.Parse(contextTypeName, dic);
            var method = typeof(DependencyInjectionExtensions).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(contextType, nameof(DependencyInjectionExtensions.AddMudBlazorTenantSecurityViews));
            method(services, partTypeLoadBehavior);
        }
    }
}
