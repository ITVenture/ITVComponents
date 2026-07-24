using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Extensions
{
    /// <summary>
    /// DI-Registrierung des Workflow-View-Moduls.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registriert die Routing-Assembly und die Monitoring-Handler. Der Host muss
        /// <c>WorkflowContext</c> (als <c>IDbContextFactory</c>), einen <c>IWorkflowStore</c> und
        /// eine <c>WorkflowEngine</c> bereitstellen.
        /// </summary>
        public static IServiceCollection AddWorkflowViews(this IServiceCollection services,
            AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
            {
                DefaultBehavior = TypeRegisterBehavior.Use
            };

            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);

            if (partTypeLoadBehavior.ShouldLoadType(typeof(WorkflowMonitorHandler)))
            {
                services.AddScoped<IWorkflowMonitorHandler, WorkflowMonitorHandler>();
            }

            return services;
        }
    }
}
