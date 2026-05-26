using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;

public static class DiagnosticsDependencyExtensions
{
    /// <summary>
    /// Registers the dependency-free AssemblyDiagnostics view (page <c>/Util/AssemblyDiagnostics</c>)
    /// and adds this assembly to the Blazor router. Usable on its own — needs only the base toolkit,
    /// no TenantSecurity context. The optional Configuration-Exchange tab is driven by
    /// <c>IHierarchySettings&lt;AssemblyDiagnosticsOptions&gt;</c>, i.e. configured in the database via Global-
    /// or Tenant-Settings (host must call <c>UseHierarchySettings()</c>).
    /// </summary>
    public static IServiceCollection AddMudBlazorDiagnostics(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehaviorOptions = null)
    {
        partTypeLoadBehaviorOptions ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };
        services.AddBlazorRoutingAssembly(typeof(DiagnosticsDependencyExtensions).Assembly, partTypeLoadBehaviorOptions);
        if (partTypeLoadBehaviorOptions.ShouldLoadType(typeof(AssemblyDiagnosticsAdminHandler)))
        {
            services.AddScoped<IAssemblyDiagnosticsAdminHandler, AssemblyDiagnosticsAdminHandler>();
        }

        return services;
    }
}
