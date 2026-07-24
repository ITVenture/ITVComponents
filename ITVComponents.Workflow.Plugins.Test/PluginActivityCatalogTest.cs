using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Workflow.Activities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.Test
{
    /// <summary>
    /// Eine dekorierte Aktivitaet fuers Katalog-Testing. Der Konstruktor wird bewusst mitgezaehlt,
    /// damit der Test beweisen kann, dass der Katalog NICHT instanziert.
    /// </summary>
    [WorkflowActivity(DisplayName = "Send mail", Description = "Sends an email")]
    [ActivityParameter("to", Kind = ActivityParameterKind.String, Required = true, Description = "Recipient", Order = 1)]
    [ActivityParameter("priority", Kind = ActivityParameterKind.Picklist, Values = new[] { "low", "high" }, Order = 2)]
    [ActivityParameter("body", Kind = ActivityParameterKind.Multiline, Order = 3)]
    [ActivityParameter("queue", Kind = ActivityParameterKind.Picklist, ValuesProvider = nameof(GetQueues), Order = 4)]
    [ActivityParameter("messageId", Kind = ActivityParameterKind.String, Direction = ActivityParameterDirection.Output, Order = 5)]
    public class SendMailActivity : IActivityPlugin
    {
        /// <summary>Zaehlt, wie oft diese Aktivitaet instanziert wurde (muss beim Katalog 0 bleiben).</summary>
        public static int Constructed;

        public SendMailActivity()
        {
            Constructed++;
        }

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Execute(WorkflowActivityContext context)
        {
        }

        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Dynamischer Werte-Provider - laeuft im Katalog-Scope.</summary>
        public static IEnumerable<ActivityParameterValue> GetQueues(IPluginFactory scope)
        {
            return new[]
            {
                new ActivityParameterValue { Value = "q1", Label = "Queue 1" },
                new ActivityParameterValue { Value = "q2", Label = "Queue 2" }
            };
        }
    }

    /// <summary>Ein Test-Loader, der die dekorierte Aktivitaet als Scoped-Plugin bereitstellt.</summary>
    public class CatalogTestLoader : IDynamicLoader
    {
        private const string Ctor = "[wftest]<ITVComponents.Workflow.Plugins.Test.SendMailActivity>";

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

        public bool HasScopedPlugin(string pluginName) => pluginName == "sendmail";

        public PluginConfigurationItem GetScopedPlugin(string pluginName)
            => pluginName == "sendmail"
                ? new PluginConfigurationItem { Name = "sendmail", ConstructionString = Ctor }
                : null;

        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames()
            => new[] { new PluginConfigurationItem { Name = "sendmail", ConstructionString = Ctor } };
    }

    /// <summary>
    /// Prueft den <see cref="PluginActivityCatalog"/>: Typen und Parameter kommen aus den
    /// Klassen-Attributen, ohne dass die Aktivitaet instanziert wird; statische und dynamische
    /// Auswahlwerte funktionieren.
    /// </summary>
    [TestClass]
    public class PluginActivityCatalogTest
    {
        private PluginFactory factory;
        private PluginActivityCatalog catalog;

        [TestInitialize]
        public void Setup()
        {
            SendMailActivity.Constructed = 0;
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("wftest", typeof(SendMailActivity).Assembly);
            factory.LoadPlugin<CatalogTestLoader>("loader",
                "[wftest]<ITVComponents.Workflow.Plugins.Test.CatalogTestLoader>");
            catalog = new PluginActivityCatalog(factory);
        }

        [TestCleanup]
        public void Cleanup() => factory?.Dispose();

        [TestMethod]
        public void GetActivityTypes_FindsDecoratedActivity_WithoutInstantiating()
        {
            var types = catalog.GetActivityTypes();
            ActivityTypeInfo t = types.SingleOrDefault(x => x.ActivityRef == "sendmail");
            Assert.IsNotNull(t);
            Assert.AreEqual("Send mail", t.DisplayName);
            Assert.AreEqual(0, SendMailActivity.Constructed, "the catalog must not instantiate the activity");
        }

        [TestMethod]
        public void GetParameters_ReadsAttributes()
        {
            var ps = catalog.GetParameters("sendmail");
            Assert.AreEqual(5, ps.Count);
            Assert.AreEqual("to", ps[0].Name); // Order = 1 -> first

            Assert.IsTrue(ps.Single(p => p.Name == "to").Required);
            Assert.AreEqual(ActivityParameterKind.Multiline, ps.Single(p => p.Name == "body").Kind);
            Assert.AreEqual(ActivityParameterDirection.Output, ps.Single(p => p.Name == "messageId").Direction);

            ActivityParameter priority = ps.Single(p => p.Name == "priority");
            Assert.AreEqual(2, priority.Values.Count);
            Assert.IsFalse(priority.HasDynamicValues);

            ActivityParameter queue = ps.Single(p => p.Name == "queue");
            Assert.IsTrue(queue.HasDynamicValues);
            Assert.AreEqual(0, queue.Values.Count, "dynamic values are fetched lazily, not inline");

            Assert.AreEqual(0, SendMailActivity.Constructed);
        }

        [TestMethod]
        public void GetValidValues_StaticPicklist()
        {
            var vals = catalog.GetValidValues("sendmail", "priority");
            CollectionAssert.AreEquivalent(new[] { "low", "high" }, vals.Select(v => (string)v.Value).ToArray());
        }

        [TestMethod]
        public void GetValidValues_DynamicProviderRunsAndReturns()
        {
            var vals = catalog.GetValidValues("sendmail", "queue");
            Assert.AreEqual(2, vals.Count);
            Assert.AreEqual("Queue 1", vals.Single(v => (string)v.Value == "q1").Label);
        }

        [TestMethod]
        public void GetParameters_UnknownActivity_IsEmpty()
        {
            Assert.AreEqual(0, catalog.GetParameters("does-not-exist").Count);
        }
    }
}
