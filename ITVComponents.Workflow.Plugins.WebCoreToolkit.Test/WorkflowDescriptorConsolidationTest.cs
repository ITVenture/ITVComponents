#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.WebWorker;
using ITVComponents.Workflow.WebWorker.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit.Test
{
    /// <summary>
    /// Prueft, wann die Discovery die je Mandant erzeugten Poll-Beschreibungen zu EINER zusammenfasst.
    /// </summary>
    /// <remarks>
    /// Der Massstab ist die <b>Umgebung</b> (= eine Ablage), nicht die Zahl der Mandanten, die sie
    /// tragen. Fasst die Discovery nicht zusammen, laeuft die mandantenuebergreifende Arbeit
    /// (Zeitplaene, Nachrichten, Fristen) je Mandant einmal an - dieselbe Nachricht eroeffnet dann n
    /// Vorgaenge statt einem. Dass ein Mandant die Umgebung nicht traegt, ist dabei KEIN Grund zu
    /// trennen: seine Vorgaenge in derselben Ablage wurden auch vorher schon von den filterfreien
    /// Deskriptoren der anderen angetrieben. Ob er fachlich laufen darf, entscheidet das Feature-Gate
    /// bei jedem Feuern.
    /// </remarks>
    [TestClass]
    public class WorkflowDescriptorConsolidationTest
    {
        private static WorkflowEnvironmentDiscovery NewDiscovery()
            => new WorkflowEnvironmentDiscovery(null!, new WorkflowWorkerOptions(),
                NullLogger<WorkflowEnvironmentDiscovery>.Instance);

        private static DescriptorSpec Spec(string tenant, string env = "prod", string? store = "WorkflowStore",
            params string[] targets)
            => new DescriptorSpec(
                Key: $"{env}|{tenant}",
                EnvironmentName: env,
                TenantId: tenant,
                StorePluginName: store,
                HostTargets: targets,
                MaxLinger: TimeSpan.FromSeconds(30));

        [TestMethod]
        public void TenantsSharingAnEnvironment_BecomeOneCrossTenantDescriptor()
        {
            // Der Regelfall: eine global konfigurierte Umgebung, die niemand ueberschreibt.
            var perTenant = new List<DescriptorSpec>
            {
                Spec("adm"), Spec("acme"), Spec("beta"), Spec("gamma"), Spec("delta")
            };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(perTenant);

            Assert.AreEqual(1, result.Count,
                "five tenants sharing one environment must be driven by ONE descriptor - otherwise the "
                + "cross-tenant pickup runs five times over the same database.");
            Assert.IsNull(result[0].TenantId,
                "and it must run tenant-free: the tenant of each case comes from its own instance.");
            Assert.AreEqual("prod", result[0].Key, "the key drops the tenant part with it.");
        }

        [TestMethod]
        public void SubsetOfTenants_StillBecomesOneDescriptor()
        {
            // DER Fall, der im Mehr-Feature-Betrieb die Regel ist: nur ein Teil der Mandanten hat das
            // Workflow-Feature. Das ist KEIN Grund, die Umgebung mehrfach zu fahren - wer sie nicht hat,
            // wurde von den filterfreien Deskriptoren der anderen ohnehin mitgefahren.
            var perTenant = new List<DescriptorSpec> { Spec("adm"), Spec("acme") };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(perTenant);

            Assert.AreEqual(1, result.Count,
                "two of five tenants having the environment is still ONE environment - and one database.");
            Assert.IsNull(result[0].TenantId);
        }

        [TestMethod]
        public void DifferentStore_StaysSeparate()
        {
            // Ein Mandant faehrt eine eigene Ablage. Seine Beschreibung darf nicht mit den anderen
            // verschmelzen - sie zeigt auf eine andere Datenbank.
            var perTenant = new List<DescriptorSpec>
            {
                Spec("adm"), Spec("acme"), Spec("beta", store: "OwnWorkflowStore")
            };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(perTenant);

            Assert.AreEqual(2, result.Count, "two distinct stores means two descriptors.");
            CollectionAssert.AreEquivalent(new[] { "WorkflowStore", "OwnWorkflowStore" },
                result.Select(r => r.StorePluginName).ToArray());
            Assert.IsTrue(result.All(r => r.TenantId == null), "both run tenant-free.");

            // Gleicher Umgebungs-NAME, verschiedene Ablagen: die Schluessel muessen sich unterscheiden,
            // sonst verdraengt in der Deskriptor-Tabelle einer den anderen - und eine der beiden Ablagen
            // liefe gar nicht mehr.
            Assert.AreEqual(2, result.Select(r => r.Key).Distinct().Count(),
                "distinct keys - otherwise one of the two stores silently stops being polled.");
        }

        [TestMethod]
        public void DifferentHostTargets_AreADifferentConfiguration()
        {
            // Dieselbe Ablage, aber andere Ausfuehrungs-Ziele: das ist eine andere Arbeit und darf nicht
            // zusammenfallen. Die REIHENFOLGE der Ziele ist dagegen keine Aussage.
            var perTenant = new List<DescriptorSpec>
            {
                Spec("adm", targets: new[] { "a", "b" }),
                Spec("acme", targets: new[] { "b", "a" }),
                Spec("beta", targets: new[] { "a", "c" })
            };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(perTenant);

            Assert.AreEqual(2, result.Count,
                "'adm' and 'acme' want the same work (order is not a difference), 'beta' wants another.");
        }

        [TestMethod]
        public void SingleTenant_BecomesTheEnvironmentDescriptor()
        {
            // Auch ein einzelner Mandant fuehrt zum Umgebungs-Deskriptor: die Umgebung ist die Ablage,
            // und dort soll genau einer pollen.
            var perTenant = new List<DescriptorSpec> { Spec("adm") };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(perTenant);

            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result[0].TenantId);
            Assert.AreEqual("prod", result[0].Key);
        }

        [TestMethod]
        public void GlobalSpecs_PassThroughUntouched()
        {
            // Ein Host ohne Mandanten-Modell liefert bereits tenant-freie Beschreibungen.
            var specs = new List<DescriptorSpec>
            {
                new DescriptorSpec("prod", "prod", null, "WorkflowStore", Array.Empty<string>(),
                    TimeSpan.FromSeconds(30))
            };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(specs);

            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result[0].TenantId);
        }

        [TestMethod]
        public void DifferentEnvironments_AreNeverMerged()
        {
            // Zwei Umgebungen sind zwei Ablagen - auch wenn derselbe Mandant beide traegt.
            var perTenant = new List<DescriptorSpec>
            {
                Spec("adm", env: "prod"), Spec("adm", env: "archive", store: "ArchiveStore"),
                Spec("acme", env: "prod")
            };

            IReadOnlyList<DescriptorSpec> result = NewDiscovery().Consolidate(perTenant);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEquivalent(new[] { "prod", "archive" },
                result.Select(r => r.EnvironmentName).ToArray());
        }
    }
}
