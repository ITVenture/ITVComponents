using System;
using ITVComponents.Workflow.WebWorker.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.Workflow.WebWorker.Extensions
{
    /// <summary>DI-Verdrahtung des Workflow-Background-Workers.</summary>
    public static class WorkflowWorkerServiceCollectionExtensions
    {
        /// <summary>
        /// Registriert den Workflow-Background-Worker als Hosted-Service. Voraussetzung im Host: die
        /// scope-owned <c>WorkflowContext</c>-Dependency(en) (pro Umgebung unter Namen =
        /// <c>WorkflowStorePluginName</c>), ein <c>IActivityHost</c> und - im Tenant-Betrieb - der
        /// automatisch registrierte <c>IAllTenantsReader</c> (kommt ueber die Security-Kontexte).
        /// </summary>
        /// <remarks>
        /// Der zurueckgegebene Hosted-Service ist zugleich <see cref="IWorkflowWorkerWake"/> (dieselbe
        /// Singleton-Instanz), damit signal-/enqueue-nahe Stellen einen Deskriptor sofort wecken koennen.
        /// </remarks>
        public static IServiceCollection AddWorkflowWebWorker(this IServiceCollection services,
            Action<WorkflowWorkerOptions>? configure = null)
        {
            var opt = new WorkflowWorkerOptions();
            configure?.Invoke(opt);

            services.AddSingleton(opt);
            services.AddSingleton<WorkflowEnvironmentDiscovery>();
            services.AddSingleton<WorkflowWorkerService>();
            services.AddSingleton<IWorkflowWorkerWake>(sp => sp.GetRequiredService<WorkflowWorkerService>());
            services.AddHostedService(sp => sp.GetRequiredService<WorkflowWorkerService>());
            return services;
        }
    }
}
