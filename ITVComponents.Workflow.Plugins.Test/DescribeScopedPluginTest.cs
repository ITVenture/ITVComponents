using ITVComponents.Plugins;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.Test
{
    /// <summary>
    /// Prueft <see cref="PluginFactory.DescribeScopedPlugin"/>: ein Bereichs-Plugin wird beschrieben, ohne dass
    /// sein Konstruktor laeuft. Darauf stuetzt sich die Verfuegbarkeitspruefung des IPC-FactoryWrappers - eine
    /// Pruefung darf keine Geraete oeffnen.
    /// </summary>
    [TestClass]
    public class DescribeScopedPluginTest
    {
        private PluginFactory factory;

        [TestInitialize]
        public void Setup()
        {
            SetVariableActivity.Reset();
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("wftest", typeof(SetVariableActivity).Assembly);
            factory.LoadPlugin<TestScopedActivityLoader>("activityLoader",
                "[wftest]<ITVComponents.Workflow.Plugins.Test.TestScopedActivityLoader>");
        }

        [TestCleanup]
        public void Cleanup()
        {
            factory?.Dispose();
        }

        [TestMethod]
        public void KnownScopedPluginIsDescribedWithoutBeingConstructed()
        {
            var described = factory.DescribeScopedPlugin("setvar");

            Assert.IsNotNull(described, "A name the loader knows as scoped plugin must be described.");
            Assert.AreEqual(typeof(SetVariableActivity), described.PluginType);
            Assert.AreEqual(0, SetVariableActivity.Constructed, "Describing must not run the constructor.");
            Assert.IsFalse(factory.Contains("setvar"), "Describing must not register an instance either.");
        }

        [TestMethod]
        public void UnknownNameIsNotDescribed()
        {
            Assert.IsNull(factory.DescribeScopedPlugin("nosuchplugin"));
            Assert.IsNull(factory.DescribeScopedPlugin(null));
        }

        [TestMethod]
        public void DescribedPluginIsStillResolvableInAScope()
        {
            factory.DescribeScopedPlugin("setvar");

            using (var scope = factory.NewScope(null, null, false))
            {
                Assert.IsInstanceOfType(scope["setvar", true], typeof(SetVariableActivity));
            }

            Assert.AreEqual(1, SetVariableActivity.Constructed);
            Assert.AreEqual(1, SetVariableActivity.DisposeCount, "The scoped instance belongs to the scope.");
        }
    }
}
