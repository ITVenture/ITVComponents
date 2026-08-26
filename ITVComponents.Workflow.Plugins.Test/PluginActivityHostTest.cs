using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Workflow;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.Test
{
    /// <summary>
    /// Eine Aktivitaet, die als Plugin geladen wird. Zaehlt Konstruktion und Freigabe mit, damit
    /// die Tests das On-demand-Laden und das Freigeben beim Scope-Ende nachweisen koennen.
    /// </summary>
    public class SetVariableActivity : IActivityPlugin
    {
        /// <summary>Wie oft eine Instanz dieser Aktivitaet konstruiert wurde.</summary>
        public static int Constructed;

        /// <summary>Wie oft eine Instanz dieser Aktivitaet freigegeben wurde.</summary>
        public static int DisposeCount;

        private bool disposed;

        /// <summary>Setzt die Zaehler zurueck.</summary>
        public static void Reset()
        {
            Constructed = 0;
            DisposeCount = 0;
        }

        /// <summary>Konstruktor - wird von der PluginFactory ueber den Konstruktions-String aufgerufen.</summary>
        public SetVariableActivity()
        {
            Interlocked.Increment(ref Constructed);
        }

        /// <inheritdoc/>
        public string UniqueName { get; set; }

        /// <inheritdoc/>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public void Execute(WorkflowActivityContext context)
        {
            context.Variables["ran"] =
                (context.Variables.TryGetValue("ran", out object r) ? (int)r : 0) + 1;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Interlocked.Increment(ref DisposeCount);
                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Ein Test-<see cref="IDynamicLoader"/>, der die Scoped-Plugin-Definitionen der Aktivitaeten
    /// bereitstellt - so, wie es in Produktion der datenbankgetriebene Loader tut. Der
    /// <see cref="PluginActivityHost"/> laesst die Factory ueber diesen Loader aufloesen; der
    /// Loader traegt den Konstruktions-String je ActivityRef.
    /// </summary>
    public class TestScopedActivityLoader : IDynamicLoader
    {
        private static readonly Dictionary<string, string> ScopedPlugins = new Dictionary<string, string>
        {
            { "setvar", "[wftest]<ITVComponents.Workflow.Plugins.Test.SetVariableActivity>" }
        };

        /// <inheritdoc/>
        public string UniqueName { get; set; }

        /// <inheritdoc/>
        public event EventHandler Disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true)
        {
            return Array.Empty<string>();
        }

        /// <inheritdoc/>
        public bool HasParamsFor(string uniqueName)
        {
            return false;
        }

        /// <inheritdoc/>
        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments,
            Dictionary<string, object> customVariables, StringFormatProvider formatter)
        {
        }

        /// <inheritdoc/>
        public bool HasScopedPlugin(string pluginName)
        {
            return ScopedPlugins.ContainsKey(pluginName);
        }

        /// <inheritdoc/>
        public PluginConfigurationItem GetScopedPlugin(string pluginName)
        {
            return ScopedPlugins.TryGetValue(pluginName, out string ctor)
                ? new PluginConfigurationItem { Name = pluginName, ConstructionString = ctor }
                : null;
        }

        /// <inheritdoc/>
        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames()
        {
            return ScopedPlugins.Select(kv => new PluginConfigurationItem
            {
                Name = kv.Key,
                ConstructionString = kv.Value
            });
        }
    }

    /// <summary>
    /// Prueft die Plugin-basierte Aktivitaets-Aufloesung: Schritte werden on demand aus der
    /// PluginFactory geladen (die Factory loest sie selbst ueber den <see cref="IDynamicLoader"/>
    /// auf) und beim Schliessen des Vortriebs-Scopes wieder freigegeben.
    /// </summary>
    [TestClass]
    public class PluginActivityHostTest
    {
        private PluginFactory factory;
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            SetVariableActivity.Reset();
            // PerAsyncContext: der Scope ueberlebt Ausfuehrungsgrenzen (die Engine laeuft ggf. auf
            // Workern). Die Test-Assembly wird registriert, damit die Konstruktions-Strings sie ohne
            // Datei-Probing finden.
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("wftest", typeof(SetVariableActivity).Assembly);

            // Den DynamicLoader auf Factory-Ebene laden (wie in Produktion) - der Scope sieht ihn und
            // loest 'setvar' selbst auf. Kein ActivityRef->Konstruktions-String-Callback mehr im Host.
            factory.LoadPlugin<TestScopedActivityLoader>("activityLoader",
                "[wftest]<ITVComponents.Workflow.Plugins.Test.TestScopedActivityLoader>");

            var host = new PluginActivityHost(factory);

            store = new InMemoryWorkflowStore();
            engine = new WorkflowEngine(store, host);
        }

        [TestCleanup]
        public void Cleanup()
        {
            factory?.Dispose();
        }

        [TestMethod]
        public void PluginActivityRunsAndIsReleasedAfterAdvance()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "one",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "setvar" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->a", SourceId = "s", TargetId = "a" },
                    new SequenceFlow { Id = "a->e", SourceId = "a", TargetId = "e" }
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("one");

            Assert.AreEqual(WorkflowStatus.Completed, instance.Status);
            Assert.AreEqual(1, instance.Variables["ran"]);
            Assert.AreEqual(1, SetVariableActivity.Constructed, "The plugin should be loaded once, on demand.");
            Assert.AreEqual(1, SetVariableActivity.DisposeCount,
                "The plugin should be released when the advance scope closed.");
        }

        [TestMethod]
        public void PluginIsLoadedFreshPerAdvance()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "two",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a1", ActivityRef = "setvar" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new AutomatedActivityNode { Id = "a2", ActivityRef = "setvar" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->a1", SourceId = "s", TargetId = "a1" },
                    new SequenceFlow { Id = "a1->w", SourceId = "a1", TargetId = "w" },
                    new SequenceFlow { Id = "w->a2", SourceId = "w", TargetId = "a2" },
                    new SequenceFlow { Id = "a2->e", SourceId = "a2", TargetId = "e" }
                }
            });

            WorkflowInstance instance = engine.StartWorkflow("two");

            // Erster Vortrieb: Aktivitaet lief, dann Wartepunkt -> Scope geschlossen, Plugin freigegeben.
            Assert.AreEqual(WorkflowStatus.Waiting, instance.Status);
            Assert.AreEqual(1, instance.Variables["ran"]);
            Assert.AreEqual(1, SetVariableActivity.Constructed);
            Assert.AreEqual(1, SetVariableActivity.DisposeCount);

            engine.SignalWorkflow(instance.Id, "go");

            // Zweiter Vortrieb: frischer Scope, frisch geladen, wieder freigegeben.
            Assert.AreEqual(WorkflowStatus.Completed, store.GetInstance(instance.Id).Status);
            Assert.AreEqual(2, store.GetInstance(instance.Id).Variables["ran"]);
            Assert.AreEqual(2, SetVariableActivity.Constructed, "Each advance loads its own plugin instance.");
            Assert.AreEqual(2, SetVariableActivity.DisposeCount);
        }

        [TestMethod]
        public void NoPluginIsLoadedWhenNoActivityRuns()
        {
            store.SaveDefinition(new WorkflowDefinition
            {
                TechnicalName = "waitonly",
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new WaitNode { Id = "w", SignalName = "go" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "s->w", SourceId = "s", TargetId = "w" },
                    new SequenceFlow { Id = "w->e", SourceId = "w", TargetId = "e" }
                }
            });

            engine.StartWorkflow("waitonly");

            // Kein automatischer Schritt -> der Scope wird gar nicht erst geoeffnet.
            Assert.AreEqual(0, SetVariableActivity.Constructed, "A scope must only open when an activity is resolved.");
            Assert.AreEqual(0, SetVariableActivity.DisposeCount);
        }
    }
}
