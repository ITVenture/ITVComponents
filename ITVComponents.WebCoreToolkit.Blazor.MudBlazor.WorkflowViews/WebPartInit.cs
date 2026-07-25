using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews;

/// <summary>
/// WebPart des Workflow-View-Moduls: macht die Views auffindbar und registriert - wenn per
/// Konfiguration eingeschaltet - die Handler.
/// </summary>
[WebPart]
public static class WebPartInit
{
    /// <summary>Laedt die Modul-Konfiguration (DetailConfigPath des Hosts).</summary>
    [LoadWebPartConfig]
    public static object? LoadOptions(IConfiguration config, string path)
    {
        return config.GetSection<WorkflowViewsOptions>(path);
    }

    /// <summary>Registriert Routing-Assembly und (optional) die Handler.</summary>
    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services,
        [WebPartConfig] WorkflowViewsOptions? options,
        [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
    {
        // Ohne diese Zeile werden die @page-Seiten des Moduls vom Host-Router nicht gefunden.
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly, partTypeLoadBehavior);

        if (options is { ConfigureViews: true })
        {
            services.AddWorkflowViews(partTypeLoadBehavior, options);
        }
    }
}
