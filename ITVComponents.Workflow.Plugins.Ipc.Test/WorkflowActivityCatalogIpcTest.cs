using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Client;
using ITVComponents.InterProcessCommunication.InMemory.Server;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Workflow.Activities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.Ipc.Test
{
    /// <summary>Eine dekorierte Test-Aktivitaet (wird vom Katalog NIE instanziert).</summary>
    [WorkflowActivity(DisplayName = "Ping", Description = "Pings something")]
    [ActivityParameter("to", Kind = ActivityParameterKind.String, Required = true, Order = 1)]
    [ActivityParameter("priority", Kind = ActivityParameterKind.Picklist, Values = new[] { "low", "high" },
        Labels = new[] { "Low", "High" }, Default = "low", Order = 2)]
    [ActivityParameter("queue", Kind = ActivityParameterKind.CallbackList, ValuesProvider = "pingQueues", Order = 3)]
    [ActivityParameter("messageId", Kind = ActivityParameterKind.String,
        Direction = ActivityParameterDirection.Output, Order = 4)]
    public class PingActivity : IActivityPlugin
    {
        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Execute(WorkflowActivityContext context)
        {
        }

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Dynamischer Werte-Provider - wird vom Katalog im Backend-Scope konstruiert.</summary>
    public class PingValuesProvider : IValuesProvider
    {
        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        public IEnumerable<ActivityParameterValue> GetValues(string parameterName)
            => new[]
            {
                new ActivityParameterValue { Value = "q1", Label = "Queue 1" },
                new ActivityParameterValue { Value = "q2", Label = "Queue 2" }
            };
    }

    /// <summary>Ein Test-Loader, der Aktivitaet + Provider als Scoped-Plugins bereitstellt.</summary>
    public class PingLoader : IDynamicLoader
    {
        private static readonly Dictionary<string, string> Scoped = new Dictionary<string, string>
        {
            { "ping", "[ipctest]<ITVComponents.Workflow.Plugins.Ipc.Test.PingActivity>" },
            { "pingQueues", "[ipctest]<ITVComponents.Workflow.Plugins.Ipc.Test.PingValuesProvider>" }
        };

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true)
            => Array.Empty<string>();

        public bool HasParamsFor(string uniqueName) => false;

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments,
            Dictionary<string, object> customVariables, StringFormatProvider formatter)
        {
        }

        public bool HasScopedPlugin(string pluginName) => Scoped.ContainsKey(pluginName);

        public PluginConfigurationItem GetScopedPlugin(string pluginName)
            => Scoped.TryGetValue(pluginName, out string ctor)
                ? new PluginConfigurationItem { Name = pluginName, ConstructionString = ctor }
                : null;

        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames()
            => Scoped.Select(kv => new PluginConfigurationItem { Name = kv.Key, ConstructionString = kv.Value });
    }

    /// <summary>
    /// Prueft den IPC-Weg des Aktivitaets-Katalogs: ein <see cref="PluginActivityCatalog"/> im "Backend"
    /// wird ueber die InterProcessCommunication (InMemory-Transport, voller JSON-Round-Trip) von einem
    /// <see cref="WorkflowActivityCatalogClient"/> im "Frontend" abgefragt. Beweist zugleich, dass die
    /// Katalog-DTOs (inkl. verschachtelter Werte-Listen, object-Werte und Enums) ueber die Prozessgrenze
    /// tragen.
    /// </summary>
    [TestClass]
    public class WorkflowActivityCatalogIpcTest
    {
        private const string ServiceName = "WorkflowService";
        private const string CatalogObjectName = "workflowCatalog";

        private MessageServiceHub hub;
        private PluginFactory factory;
        private InMemoryServer server;
        private InMemoryClient client;
        private WorkflowActivityCatalogClient remoteCatalog;

        [TestInitialize]
        public void Setup()
        {
            // "Backend": Factory mit dem Loader (kennt die Aktivitaeten); daraus der Katalog. Der Katalog
            // wird dem Server direkt als benanntes Objekt uebergeben (exposedObjects-Ctor) - das exponiert
            // genau dieses Objekt, ohne den Katalog selbst als Plugin laden zu muessen.
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("ipctest", typeof(PingActivity).Assembly);
            factory.LoadPlugin<PingLoader>("loader",
                "[ipctest]<ITVComponents.Workflow.Plugins.Ipc.Test.PingLoader>");
            var catalog = new PluginActivityCatalog(factory) { UniqueName = CatalogObjectName };

            hub = new MessageServiceHub();
            var exposed = new Dictionary<string, object> { { CatalogObjectName, catalog } };
            server = new InMemoryServer((IServiceHubProvider)hub, exposed, ServiceName);
            // BaseServer ist IDeferredInit: erst Initialize() registriert den Dienst beim Hub-Broker
            // (der serviceHub-Ctor tut das nicht automatisch). Ohne das findet der Client ihn nicht.
            server.Initialize();

            // "Frontend": Client + Katalog-Proxy ueber denselben Hub.
            client = new InMemoryClient((IServiceHubProvider)hub, ServiceName, false);
            remoteCatalog = new WorkflowActivityCatalogClient(client, CatalogObjectName);
        }

        [TestCleanup]
        public void Cleanup()
        {
            remoteCatalog?.Dispose();
            client?.Dispose();
            server?.Dispose();
            factory?.Dispose();
            hub?.Dispose();
        }

        [TestMethod]
        public void GetActivityTypes_RoundTripsOverIpc()
        {
            var types = remoteCatalog.GetActivityTypes();
            ActivityTypeInfo ping = types.SingleOrDefault(t => t.ActivityRef == "ping");
            Assert.IsNotNull(ping, "the activity type must arrive over IPC.");
            Assert.AreEqual("Ping", ping.DisplayName);
            Assert.AreEqual("Pings something", ping.Description);
        }

        [TestMethod]
        public void GetParameters_RoundTripsWithNestedValuesAndEnums()
        {
            var ps = remoteCatalog.GetParameters("ping");
            Assert.AreEqual(4, ps.Count);

            ActivityParameter to = ps.Single(p => p.Name == "to");
            Assert.IsTrue(to.Required, "bool must round-trip.");
            Assert.AreEqual(ActivityParameterKind.String, to.Kind, "enum must round-trip.");

            ActivityParameter priority = ps.Single(p => p.Name == "priority");
            Assert.AreEqual(ActivityParameterKind.Picklist, priority.Kind);
            // Verschachtelte Werte-Liste (das Serialisierungs-Risiko): Wert + Label muessen ankommen.
            Assert.AreEqual(2, priority.Values.Count, "the nested static values must round-trip.");
            Assert.AreEqual("High", priority.Values.Single(v => (string)v.Value == "high").Label);
            Assert.AreEqual("low", (string)priority.Default, "the object Default must round-trip.");

            ActivityParameter messageId = ps.Single(p => p.Name == "messageId");
            Assert.AreEqual(ActivityParameterDirection.Output, messageId.Direction);

            // CallbackList-Werte kommen NICHT inline mit den Parametern.
            ActivityParameter queue = ps.Single(p => p.Name == "queue");
            Assert.AreEqual(ActivityParameterKind.CallbackList, queue.Kind);
            Assert.AreEqual(0, queue.Values.Count);
        }

        [TestMethod]
        public void GetValidValues_ConstructsBackendProviderOverIpc()
        {
            // Der Provider wird im Backend-Scope konstruiert; das Ergebnis kommt ueber IPC zurueck.
            var vals = remoteCatalog.GetValidValues("ping", "queue");
            Assert.AreEqual(2, vals.Count);
            Assert.AreEqual("Queue 1", vals.Single(v => (string)v.Value == "q1").Label);
        }

        [TestMethod]
        public void GetValidValues_StaticPicklistOverIpc()
        {
            var vals = remoteCatalog.GetValidValues("ping", "priority");
            CollectionAssert.AreEquivalent(new[] { "low", "high" }, vals.Select(v => (string)v.Value).ToArray());
        }

        [TestMethod]
        public void GetParameters_UnknownActivity_IsEmptyOverIpc()
        {
            Assert.AreEqual(0, remoteCatalog.GetParameters("does-not-exist").Count);
        }
    }
}
