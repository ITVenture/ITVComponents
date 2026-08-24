using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// <c>UseWorker</c> ein Deskriptor erzeugt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Mandanten, die dieselbe Umgebung gleich konfiguriert haben, teilen sich EINEN tenant-freien
    /// Deskriptor</b> - der Regelfall, wenn die Umgebung global konfiguriert und nirgends ueberschrieben
    /// ist. Frueher entstand je Tenant einer, mit der Begruendung "disjunkte Sichten, keine
    /// Doppelverarbeitung". Diese Begruendung traegt nicht: die Aufgriffs-Wege des Stores laufen
    /// ausdruecklich filterfrei (<c>ClaimDueTimers</c>, <c>ClaimDueScheduleTriggers</c>,
    /// <c>FindMessageTriggers</c>, die Outbox), weil der Runner mandantenuebergreifend arbeiten MUSS -
    /// sein Suchlauf geht jedem <c>WorkflowExecutionScope</c> voraus. Jeder tenant-gepinnte Deskriptor
    /// sah damit die Zeilen ALLER Mandanten, und bei n Tenants lief dieselbe Arbeit n-fach an: eine
    /// gesendete Nachricht eroeffnete n Vorgaenge, ein Zweig wurde mehrfach vorangetrieben.
    /// </para>
    /// <para>
    /// <b>Der Tenant-Kontext geht dabei nicht verloren.</b> Er gehoert nicht zum Deskriptor, sondern zur
    /// Instanz: <c>WebToolkitActivityHost.OpenScope(instance)</c> oeffnet je Vortrieb einen DI-Scope auf
    /// <c>instance.TenantId</c> samt tenant-spezifischer Plugin-Factory, und die Engine setzt zusaetzlich
    /// den ambienten <c>WorkflowExecutionScope</c>. Eine als Plugin geladene Aktivitaet und ihr
    /// tenant-gefilterter DbContext sehen also weiterhin genau den Mandanten ihrer Instanz.
    /// </para>
    /// <para>
    /// <b>Der Massstab ist die Umgebung, nicht die Gesamtheit der Mandanten</b> (siehe
    /// <see cref="Consolidate"/>): zusammengefasst wird je <i>Signatur</i> - Umgebungs-Name, Store,
    /// Ausfuehrungs-Ziele, Poll-Obergrenze. Ein Mandant, der abweicht oder gar kein <c>UseWorker</c> hat,
    /// haelt die anderen also nicht auf; er faellt lediglich nicht in deren Gruppe. Das ist Absicht: im
    /// Mehr-Feature-Betrieb ist "nur ein Teil der Mandanten hat das Workflow-Feature" die Regel, und
    /// eine Zusammenfassung, die Einstimmigkeit verlangt, griffe dort praktisch nie.
    /// </para>
    /// </remarks>
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

            return Consolidate(specs);
        }

        /// <summary>
        /// Fasst die je Tenant erzeugten Beschreibungen zu einer je <b>Umgebung</b> zusammen.
        /// </summary>
        /// <param name="perTenant">die tenant-gepinnten Beschreibungen</param>
        /// <returns>die zu fahrende Menge</returns>
        /// <remarks>
        /// <para>
        /// Der Massstab ist die Umgebung, NICHT die Menge der Mandanten, die sie tragen. Eine Umgebung ist
        /// laut ihrer eigenen Definition "ein in sich geschlossener Workflow-Datenbestand (ein Store/eine
        /// DB)", und <c>UseWorker</c> heisst "ob in DIESER UMGEBUNG der Worker laeuft". Das ist eine
        /// Aussage ueber die Ablage; sie kann fuer dieselbe Datenbank nicht je Mandant anders lauten.
        /// </para>
        /// <para>
        /// <b>Ein Mandant ohne diese Umgebung verliert dadurch nichts</b> - und gewinnt auch nichts. Seine
        /// Vorgaenge in derselben Ablage wurden auch bisher schon angetrieben, naemlich von den
        /// filterfreien Deskriptoren der anderen Mandanten; nur eben mehrfach. Die Zusammenfassung aendert
        /// die ANZAHL der Laeufe, nicht die Menge der bearbeiteten Arbeit. Ob ein Mandant fachlich laufen
        /// darf, beantwortet ohnehin nicht der Deskriptor, sondern das Feature-Gate bei jedem Feuern
        /// (<c>IWorkflowTenantFeatureGate</c>) - und das ist der richtige Ort dafuer, weil ein Abo endet,
        /// ohne dass jemand eine Einstellung anfasst.
        /// </para>
        /// <para>
        /// Verschiedene Umgebungen bleiben getrennt: unterscheiden sich Store, Ausfuehrungs-Ziele oder
        /// Poll-Obergrenze, sind es verschiedene Ablagen bzw. verschiedene Arbeit. Traegt derselbe
        /// Umgebungs-NAME bei verschiedenen Mandanten unterschiedliche Werte, ist das eine
        /// widerspruechliche Konfiguration - beide werden gefahren, und der Fall wird gemeldet.
        /// </para>
        /// </remarks>
        internal IReadOnlyList<DescriptorSpec> Consolidate(List<DescriptorSpec> perTenant)
        {
            if (perTenant.Count == 0)
            {
                return perTenant;
            }

            var merged = new List<DescriptorSpec>();
            foreach (IGrouping<string, DescriptorSpec> group in perTenant
                         .Where(s => s.TenantId != null)
                         .GroupBy(Signature, StringComparer.Ordinal))
            {
                List<DescriptorSpec> members = group.ToList();
                DescriptorSpec one = members[0];
                merged.Add(one with { Key = one.EnvironmentName ?? string.Empty, TenantId = null });
                log.LogInformation(
                    "Workflow-Worker: die Umgebung {Environment} wird von {Count} Mandant(en) gleich "
                    + "konfiguriert und als EIN mandantenuebergreifender Deskriptor gefahren. Der Mandant "
                    + "jedes Vorgangs wird beim Vortrieb aus seiner Instanz gesetzt.",
                    one.EnvironmentName ?? "(default)", members.Count);
            }

            // Derselbe Name, verschiedene Werte: die Beschreibungen bekommen unterscheidbare Schluessel,
            // sonst verdraengte in der Deskriptor-Tabelle eine die andere - eine der beiden Ablagen liefe
            // dann gar nicht mehr, und ein Vorgang, der stehen bleibt, meldet sich nicht von selbst.
            foreach (IGrouping<string, DescriptorSpec> byName in merged
                         .GroupBy(s => s.EnvironmentName ?? string.Empty, StringComparer.Ordinal))
            {
                if (byName.Count() == 1)
                {
                    continue;
                }

                log.LogWarning(
                    "Workflow-Worker: die Umgebung {Environment} ist bei verschiedenen Mandanten "
                    + "unterschiedlich konfiguriert ({Count} Varianten: abweichender Store, abweichende "
                    + "Ausfuehrungs-Ziele oder Poll-Obergrenze). Sie werden getrennt gefahren. Ist es in "
                    + "Wahrheit DIESELBE Ablage, laeuft die mandantenuebergreifende Arbeit (Zeitplaene, "
                    + "Nachrichten, Fristen) mehrfach an - dann gehoert der abweichende Teil in eine eigene "
                    + "Umgebung mit eigenem Namen.", byName.Key, byName.Count());
                foreach (DescriptorSpec variant in byName)
                {
                    merged[merged.IndexOf(variant)] =
                        variant with { Key = $"{variant.EnvironmentName}#{variant.StorePluginName}" };
                }
            }

            // Global ermittelte (tenant-freie) Beschreibungen bleiben, wie sie sind.
            merged.AddRange(perTenant.Where(s => s.TenantId == null));
            return merged;
        }

        /// <summary>
        /// Die Unterscheidungs-Merkmale einer Beschreibung OHNE den Mandanten: zwei Mandanten mit
        /// derselben Signatur wollen dieselbe Arbeit an derselben Ablage getan haben.
        /// </summary>
        private static string Signature(DescriptorSpec spec)
            => string.Join("\u001f", new[]
            {
                spec.EnvironmentName ?? string.Empty,
                spec.StorePluginName ?? string.Empty,
                spec.MaxLinger.Ticks.ToString(CultureInfo.InvariantCulture),
                // Sortiert: die Reihenfolge in der Konfiguration ist keine Aussage.
                string.Join("\u001f", spec.HostTargets.OrderBy(t => t, StringComparer.Ordinal))
            });

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
