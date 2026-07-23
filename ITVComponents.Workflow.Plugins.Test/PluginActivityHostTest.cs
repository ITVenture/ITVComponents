using System;
using System.Collections.Generic;
using System.Threading;
using ITVComponents.Plugins;
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
    /// Prueft die Plugin-basierte Aktivitaets-Aufloesung: Schritte werden on demand aus der
    /// PluginFactory geladen und beim Schliessen des Vortriebs-Scopes wieder freigegeben.
    /// </summary>
    [TestClass]
    public class PluginActivityHostTest
    {
        private const string Ctor = "[wftest]<ITVComponents.Workflow.Plugins.Test.SetVariableActivity>";

        private PluginFactory factory;
        private InMemoryWorkflowStore store;
        private WorkflowEngine engine;

        [TestInitialize]
        public void Setup()
        {
            SetVariableActivity.Reset();
            // PerAsyncContext: der Scope ueberlebt Ausfuehrungsgrenzen (die Engine laeuft ggf. auf
            // Workern). Die Test-Assembly wird registriert, damit der Konstruktions-String sie ohne
            // Datei-Probing findet.
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("wftest", typeof(SetVariableActivity).Assembly);

            var host = new PluginActivityHost(factory,
                new Dictionary<string, string> { { "setvar", Ctor } });

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
                Id = "one",
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
                Id = "two",
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
                Id = "waitonly",
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
