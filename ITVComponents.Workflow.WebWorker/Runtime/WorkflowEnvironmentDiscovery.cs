using System;
using System.Collections.Generic;
using System.Threading;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.Workflow.WebWorker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.Workflow.WebWorker.Runtime
{
    /// <summary>
    /// Der langsame Teil: geht (selten, per Refresh) einmal alle Tenants durch und ermittelt die GEWUENSCHTE
    /// Menge an Poll-Deskriptoren. Fuer jeden Tenant wird SEINE aufgeloeste Umgebungs-Config gelesen
    /// (scoped-vor-global via <see cref="IHierarchySettings{TSettings}"/>) und je Umgebung mit
    /// <c>UseWorker</c> ein tenant-gebundener Deskriptor erzeugt. Ob eine Umgebung global geerbt oder per
    /// Tenant ueberschrieben ist, spielt fuer den Runner keine Rolle - jeder Tenant bekommt seinen eigenen,
    /// tenant-gepinnten Deskriptor (disjunkte Sichten, keine Doppelverarbeitung).
    /// </summary>
    public sealed class WorkflowEnvironmentDiscovery
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly WorkflowWorkerOptions opt;
        private readonly ILogger<WorkflowEnvironmentDiscovery> log;

        public WorkflowEnvironmentDiscovery(IServiceScopeFactory scopeFactory, WorkflowWorkerOptions opt,
            ILogger<WorkflowEnvironmentDiscovery> log)
        {
            this.scopeFactory = scopeFactory;
            this.opt = opt;
            this.log = log;
        }

        /// <summary>Ermittelt die aktuell gewuenschte Deskriptor-Menge.</summary>
        public IReadOnlyList<DescriptorSpec> Discover(CancellationToken ct)
        {
            var specs = new List<DescriptorSpec>();

            IReadOnlyList<TenantIdentity> tenants;
            using (IServiceScope scope = scopeFactory.CreateScope())
            {
                IServiceProvider sp = scope.ServiceProvider;
                sp.PrepareEmptyContext(out _); // anonym -> Tenant-Tabelle ungefiltert lesbar; globale Settings

                IAllTenantsReader? tenantReader = sp.GetService<IAllTenantsReader>();
                if (tenantReader == null)
                {
                    // Kein Tenant-Modell im Host: die einzelne, global konfigurierte Umgebung(en) filterfrei fahren.
                    WorkflowEnvironmentSettings? globalSettings =
                        sp.GetService<IHierarchySettings<WorkflowEnvironmentSettings>>()?.ValueOrDefault;
                    AddWorkerEnvironments(specs, tenantId: null, globalSettings);
                    return specs;
                }

                tenants = tenantReader.ReadAllTenants();
            }

            foreach (TenantIdentity tenant in tenants)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(tenant.TenantName))
                {
                    continue;
                }

                using IServiceScope tScope = scopeFactory.CreateScope();
                // Tenant-gepinnter, authentifizierter Hintergrund-Kontext -> die Tenant-Query-Filter greifen,
                // die scoped Settings werden aus SEINER Sicht aufgeloest.
                tScope.ServiceProvider.PrepareBackgroundContext(opt.BackgroundUserName, tenant.TenantName, out _);
                WorkflowEnvironmentSettings? settings = tScope.ServiceProvider
                    .GetService<IHierarchySettings<WorkflowEnvironmentSettings>>()?.ValueOrDefault;
                AddWorkerEnvironments(specs, tenant.TenantName, settings);
            }

            return specs;
        }

        private void AddWorkerEnvironments(List<DescriptorSpec> specs, string? tenantId,
            WorkflowEnvironmentSettings? settings)
        {
            if (settings?.Environments == null)
            {
                return;
            }

            foreach (WorkflowEnvironment env in settings.Environments)
            {
                if (!env.UseWorker)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(env.Name))
                {
                    log.LogWarning(
                        "Workflow-Worker-Umgebung ohne Namen (Tenant {Tenant}) uebersprungen - eine Umgebung mit " +
                        "UseWorker braucht einen eindeutigen Namen.", tenantId ?? "(global)");
                    continue;
                }

                string key = tenantId == null ? env.Name! : $"{env.Name}|{tenantId}";
                var hostTargets = new List<string>();
                foreach (WorkflowEnvironmentInstance inst in env.Instances)
                {
                    if (!string.IsNullOrEmpty(inst.Name))
                    {
                        hostTargets.Add(inst.Name!);
                    }
                }

                TimeSpan maxLinger = env.MaxLingerSeconds is int s && s > 0
                    ? TimeSpan.FromSeconds(s)
                    : opt.MaxPollInterval;

                specs.Add(new DescriptorSpec(
                    Key: key,
                    EnvironmentName: env.Name,
                    TenantId: tenantId,
                    StorePluginName: env.WorkflowStorePluginName,
                    HostTargets: hostTargets,
                    MaxLinger: maxLinger));
            }
        }
    }
}
