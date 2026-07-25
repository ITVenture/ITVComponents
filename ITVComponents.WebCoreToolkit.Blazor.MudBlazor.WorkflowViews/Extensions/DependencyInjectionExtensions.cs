using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options;
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
        /// eine <c>WorkflowEngine</c> bereitstellen. Ueber <paramref name="options"/> laesst sich die
        /// Signal-Zustellung waehlen (inline vs. store-only/Runner - siehe
        /// <see cref="WorkflowViewsOptions.SignalDelivery"/>).
        /// </summary>
        public static IServiceCollection AddWorkflowViews(this IServiceCollection services,
            AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior, WorkflowViewsOptions? options = null)
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
            {
                DefaultBehavior = TypeRegisterBehavior.Use
            };

            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);

            if (partTypeLoadBehavior.ShouldLoadType(typeof(WorkflowMonitorHandler)))
            {
                // Signal-Zustellung nach Konfiguration: inline (Web-Only, Standard) vs. store-only, dann
                // treibt ein (Backend-)Runner voran (getrennte Deployments).
                if (options?.SignalDelivery == WorkflowSignalDelivery.Runner)
                {
                    services.AddScoped<IWorkflowMonitorHandler, SplitWorkflowMonitorHandler>();
                }
                else
                {
                    services.AddScoped<IWorkflowMonitorHandler, WorkflowMonitorHandler>();
                }
            }

            if (partTypeLoadBehavior.ShouldLoadType(typeof(WorkflowDesignHandler)))
            {
                services.AddScoped<IWorkflowDesignHandler, WorkflowDesignHandler>();
            }

            return services;
        }
    }
}
