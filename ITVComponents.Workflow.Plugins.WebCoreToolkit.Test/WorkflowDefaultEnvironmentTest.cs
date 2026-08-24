#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.Workflow.WebWorker;
using ITVComponents.Workflow.WebWorker.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit.Test
{
    /// <summary>
    /// Prueft den Web-Only-Fall: ist NIRGENDS eine Workflow-Umgebung konfiguriert, faehrt der Worker
    /// trotzdem - mit genau EINEM mandantenuebergreifenden Deskriptor auf der Standard-Ablage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Fall ist der haeufigste (ein Prozess, eine Datenbank, keine Ausfuehrungs-Ziele) und war der
    /// einzige, in dem der Worker kommentarlos nichts tat: ohne <c>Environments</c>-Sektion stieg die
    /// Discovery aus, es entstand kein Deskriptor, und der Host sah einen laufenden Hosted-Service, der
    /// keine Instanz vortrieb. Das sieht aus wie ein kaputter Workflow und ist keiner.
    /// </para>
    /// <para>
    /// Die beiden Zusicherungen unten sind die tragenden: <b>tenant-frei</b>, weil der Suchlauf des
    /// Runners jedem <c>WorkflowExecutionScope</c> vorausgeht und die Instanzen aller Mandanten finden
    /// muss - ein tenant-gepinnter Deskriptor wuerde den Kontext auf einen Mandanten festlegen. Und
    /// <b>ohne Store-Plugin</b>, weil genau daran der Worker entscheidet, seinen Store aus der
    /// <c>IDbContextFactory&lt;WorkflowContext&gt;</c> zu bauen - dem einzigen wirklich filterfreien Weg.
    /// Mit einem gefilterten Kontext verwuerfe <c>EfWorkflowStore.LoadInstances</c> jede Zeile, die einem
    /// Mandanten gehoert, und zwar fuer ALLE Aufgriffs-Wege, nicht nur die lauffaehigen Instanzen.
    /// </para>
    /// </remarks>
    [TestClass]
    public class WorkflowDefaultEnvironmentTest
    {
        /// <summary>
        /// Ein Anbieter ohne <c>IAllTenantsReader</c> und ohne Umgebungs-Einstellungen - also genau der
        /// Host, der nichts konfiguriert hat.
        /// </summary>
        private static ServiceProvider BuildBareProvider()
        {
            var services = new ServiceCollection();
            services.AddScoped<IHttpContextAccessor, TestHttpContextAccessor>();
            services.AddScoped<IPermissionScope, TestPermissionScope>();
            services.AddScoped<IContextUserProvider, DefaultContextUserProvider>();
            return services.BuildServiceProvider();
        }

        private static WorkflowEnvironmentDiscovery NewDiscovery(ServiceProvider provider)
            => new WorkflowEnvironmentDiscovery(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new WorkflowWorkerOptions(),
                NullLogger<WorkflowEnvironmentDiscovery>.Instance);

        [TestMethod]
        public void Discover_NothingConfigured_YieldsExactlyOneDescriptor()
        {
            using ServiceProvider provider = BuildBareProvider();

            IReadOnlyList<DescriptorSpec> specs = NewDiscovery(provider).Discover(CancellationToken.None);

            Assert.AreEqual(1, specs.Count,
                "Ohne konfigurierte Umgebung muss genau ein Standard-Deskriptor entstehen - sonst laeuft "
                + "der Worker leer, ohne dass es jemand sieht.");
        }

        [TestMethod]
        public void Discover_NothingConfigured_DescriptorIsTenantFree()
        {
            using ServiceProvider provider = BuildBareProvider();

            DescriptorSpec spec = NewDiscovery(provider).Discover(CancellationToken.None).Single();

            Assert.IsNull(spec.TenantId,
                "Der Standard-Deskriptor muss mandantenuebergreifend sein: sein Suchlauf geht jedem "
                + "WorkflowExecutionScope voraus und muss die Instanzen ALLER Mandanten finden.");
        }

        [TestMethod]
        public void Discover_NothingConfigured_DescriptorNamesNoStorePlugin()
        {
            using ServiceProvider provider = BuildBareProvider();

            DescriptorSpec spec = NewDiscovery(provider).Discover(CancellationToken.None).Single();

            Assert.IsTrue(string.IsNullOrEmpty(spec.StorePluginName),
                "Ohne genannten Store nimmt der Worker die IDbContextFactory<WorkflowContext> - den "
                + "filterfreien Weg. Steht hier ein Plugin-Name, leaset er stattdessen den (im Web "
                + "tenant-gefilterten) Kontext der Ansichten und findet nichts, was einem Mandanten gehoert.");
        }

        [TestMethod]
        public void Discover_NothingConfigured_DescriptorServesNoExecutionTargets()
        {
            using ServiceProvider provider = BuildBareProvider();

            DescriptorSpec spec = NewDiscovery(provider).Discover(CancellationToken.None).Single();

            Assert.AreEqual(0, spec.HostTargets.Count,
                "Web-Only heisst: keine verteilten Ausfuehrungs-Ziele. Ein Ziel hier wuerde Zweige "
                + "aufnehmen, die auf einen anderen Runner warten.");
        }
    }
}
