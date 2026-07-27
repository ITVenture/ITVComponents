using System;
using ITVComponents.Plugins;
using ITVComponents.Workflow.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.Test
{
    /// <summary>
    /// Ein Plugin, das die geteilte Laufzeit-Umgebung als Property erwartet. Der Getter dient nur dem
    /// Test - das Interface verlangt nur den Setter.
    /// </summary>
    public class RuntimeAwarePlugin : IPlugin, IWorkflowRuntimeAware
    {
        /// <inheritdoc/>
        public string UniqueName { get; set; }

        /// <inheritdoc/>
        public event EventHandler Disposed;

        /// <summary>Der von der Factory nach dem Zusammenbau gesetzte Laufzeit-Kontext.</summary>
        public WorkflowRuntimeContext Runtime { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Prueft, dass <see cref="PluginFactoryRuntimeExtensions.PublishWorkflowRuntime"/> jedem gebauten
    /// <see cref="IWorkflowRuntimeAware"/>-Plugin den Kontext "nach dem Zusammenbau" setzt.
    /// </summary>
    [TestClass]
    public class WorkflowRuntimePublishTest
    {
        [TestMethod]
        public void PublishWorkflowRuntime_SetsContextOnLoadedAwarePlugin()
        {
            using var factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("wftest", typeof(RuntimeAwarePlugin).Assembly);

            var context = new WorkflowRuntimeContext();
            factory.PublishWorkflowRuntime(context);

            var plugin = factory.LoadPlugin<RuntimeAwarePlugin>("aware",
                "[wftest]<ITVComponents.Workflow.Plugins.Test.RuntimeAwarePlugin>");

            Assert.AreSame(context, plugin.Runtime,
                "The factory should push the runtime context onto the aware plugin on initialization.");
            Assert.AreSame(context.Gate, plugin.Runtime.Gate);
        }
    }
}
