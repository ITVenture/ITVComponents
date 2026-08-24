using System;
using ITVComponents.Workflow.WebWorker.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.Workflow.WebWorker.Extensions
{
    /// <summary>DI-Verdrahtung des Workflow-Background-Workers.</summary>
    public static class WorkflowWorkerServiceCollectionExtensions
    {
        /// <summary>
        /// Registriert den Workflow-Background-Worker als Hosted-Service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Web-Only (der Regelfall):</b> es genuegt <c>AddWorkflowWebWorker()</c> ohne weitere
        /// Konfiguration. Voraussetzung sind eine <c>IDbContextFactory&lt;WorkflowContext&gt;</c> (der
        /// options-only-Weg, auf dem kein Tenant-Query-Filter im Modell steht) und ein
        /// <c>IActivityHost</c>. Ist keine Umgebung konfiguriert, faehrt der Worker genau EINEN
        /// mandantenuebergreifenden Deskriptor auf dieser Ablage. Ein eigener Hosted-Service im Host ist
        /// dafuer nicht noetig.
        /// </para>
        /// <para>
        /// <b>Warum die Kontext-Fabrik und nicht der Plugin-Kontext der Ansichten:</b> der Suchlauf des
        /// Runners laeuft VOR jedem <c>WorkflowExecutionScope</c> und muss die Instanzen aller Mandanten
        /// finden. Ein Kontext mit Tenant-Filter wertet den Mandanten dort als <c>null</c> aus - das ist
        /// nicht "kein Filter", sondern "TenantId IS NULL", und <c>EfWorkflowStore.LoadInstances</c>
        /// verwuerfe damit jede Zeile, die einem Mandanten gehoert. Der Worker liefe leer, ohne Fehler.
        /// Den Mandanten setzt nicht der Store, sondern der Vortrieb je Instanz.
        /// </para>
        /// <para>
        /// <b>Mehr-Umgebungen-Betrieb:</b> nennt eine Umgebung einen <c>WorkflowStorePluginName</c>, wird
        /// dieser als scope-owned <c>WorkflowContext</c>-Dependency geleast (jede Umgebung ihre eigene
        /// Ablage). Dann liegt es beim Host, dieses Plugin filterfrei zu bauen. Im Tenant-Betrieb kommt
        /// dazu der automatisch registrierte <c>IAllTenantsReader</c> (ueber die Security-Kontexte).
        /// </para>
        /// <para>
        /// Optional beruecksichtigt werden ein registrierter <c>IWorkflowHistoryFilter</c> und ein
        /// <c>IWorkflowTenantFeatureGate</c> - letzteres entscheidet bei jedem zeitgesteuerten und jedem
        /// nachrichten-getriebenen Start, ob der Mandant den Ablauf (noch) haben darf.
        /// </para>
        /// <para>
        /// Der registrierte Hosted-Service ist zugleich <see cref="IWorkflowWorkerWake"/> (dieselbe
        /// Singleton-Instanz), damit signal-/enqueue-nahe Stellen einen Deskriptor sofort wecken koennen.
        /// </para>
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
