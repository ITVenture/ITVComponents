using System;
using System.Reflection;
using ITVComponents.Plugins;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Plugins.Test
{
    /// <summary>
    /// Regression for the scoping of the <see cref="PluginFactory"/>. Three properties are pinned here:
    /// a transient loading-scope really OWNS what is loaded through it (it used to be a no-op, so "transient"
    /// plugins silently became factory-wide singletons), an EXPLICIT scope can not be bypassed, and every plugin
    /// is registered with its own transient-flag rather than the one of its last-resolved dependency.
    /// </summary>
    [TestClass]
    public class PluginScopeTest
    {
        private PluginFactory factory;

        [TestInitialize]
        public void Setup()
        {
            CountingPlugin.DisposeCount = 0;
            factory = new PluginFactory();
        }

        [TestCleanup]
        public void TearDown()
        {
            factory?.Dispose();
            factory = null;
        }

        [TestMethod]
        public void TransientLoadScope_OwnsTheLoadedPlugin()
        {
            IPluginFactory scope = factory.NewScope(null, null, true);

            var loaded = scope.LoadPlugin<CountingPlugin>("t", Ctor<CountingPlugin>(), null);

            Assert.IsNotNull(loaded, "Das Plugin konnte im transienten Ladescope nicht geladen werden.");
            Assert.IsNull(factory["t"],
                "Ein im transienten Ladescope geladenes Plugin darf NICHT in der factory-weiten Sammlung landen - genau das machte den Transient-Modus zum No-op.");

            IPlugin[] released = scope.ScopeClose();

            CollectionAssert.Contains(released, loaded,
                "Der Ladescope muss das Plugin beim Schliessen an den Aufrufer herausgeben.");
            Assert.AreEqual(0, CountingPlugin.DisposeCount,
                "Ein transienter Ladescope disponiert nicht selbst - das macht der Aufrufer (WebPluginHelper) beim ResetFactory.");
        }

        [TestMethod]
        public void TransientLoadScope_YieldsAFreshInstancePerScope()
        {
            IPluginFactory first = factory.NewScope(null, null, true);
            var one = first.LoadPlugin<CountingPlugin>("t", Ctor<CountingPlugin>(), null);
            first.ScopeClose();

            IPluginFactory second = factory.NewScope(null, null, true);
            var two = second.LoadPlugin<CountingPlugin>("t", Ctor<CountingPlugin>(), null);
            second.ScopeClose();

            Assert.AreNotSame(one, two,
                "Jeder transiente Ladescope muss eine frische Instanz liefern. Landete das Plugin in der factory-weiten Sammlung, blieb dort auch seine Lade-Promise stehen und jeder weitere Load bekam die ALTE Instanz zurueck.");
        }

        [TestMethod]
        public void ExplicitScope_IsHonoredEvenWhenTransientLoadingIsOff()
        {
            // Genau die Konstellation des Fehlers: der Aufrufer meldet "nicht transient" - trotzdem gehoert das
            // Plugin dem expliziten Scope. Frueher lief es an ihm vorbei in die factory-weite Sammlung und
            // ueberlebte ihn.
            factory.UseTransientScope = false;
            IPluginFactory scope = factory.NewScope(null, null, false);

            var loaded = scope.LoadPlugin<CountingPlugin>("x", Ctor<CountingPlugin>(), null);

            Assert.IsNotNull(loaded);
            Assert.IsNull(factory["x"],
                "Ein im expliziten Scope aufgeloestes Plugin darf nicht in der factory-weiten Sammlung liegen.");

            scope.Dispose();

            Assert.AreEqual(1, CountingPlugin.DisposeCount,
                "Ein expliziter Scope disponiert alles, was in ihm aufgeloest wurde - auch nicht-transiente Plugins.");
            Assert.IsNull(factory["x"], "Nach dem Scope-Close darf das Plugin nirgends mehr auffindbar sein.");
        }

        [TestMethod]
        public void TransientLoad_RestoresThePreviousValue()
        {
            Assert.IsTrue(factory.UseTransientScope, "Vorbedingung: der Default ist true.");

            using (factory.TransientLoad(false))
            {
                Assert.IsFalse(factory.UseTransientScope);

                // Das ist der Kern: die geschachtelte Aufloesung einer Abhaengigkeit setzt den Wert fuer SICH und
                // gibt ihn beim Verlassen zurueck - sonst wuerde das aeussere Plugin mit dem Flag seiner zuletzt
                // aufgeloesten Abhaengigkeit registriert.
                using (factory.TransientLoad(true))
                {
                    Assert.IsTrue(factory.UseTransientScope);
                }

                Assert.IsFalse(factory.UseTransientScope,
                    "Nach dem Verlassen der geschachtelten Aufloesung muss der Wert des aeusseren Ladevorgangs wieder gelten.");
            }

            Assert.IsTrue(factory.UseTransientScope);
        }

        [TestMethod]
        public void DisposingAScopedPluginAfterScopeClose_DoesNotThrow()
        {
            // Der Weg des WebPluginHelper: die Transienten werden beim Scope-Close eingesammelt und erst spaeter
            // (ResetFactory) disponiert - dann ist kein Scope mehr aktiv. Die Factory loeste ihre Sammlung frueher
            // ueber den DANN aktiven Scope auf, fand das Plugin nicht und lief in eine NullReferenceException.
            IPluginFactory scope = factory.NewScope(null, null, true);
            var loaded = scope.LoadPlugin<CountingPlugin>("t", Ctor<CountingPlugin>(), null);
            scope.ScopeClose();

            loaded.Dispose();

            Assert.AreEqual(1, CountingPlugin.DisposeCount);
        }

        [TestMethod]
        public void Scope_ResolvesPluginsOfTheOwningFactory()
        {
            factory.LoadPlugin<CountingPlugin>("global", Ctor<CountingPlugin>());
            IPluginFactory scope = factory.NewScope(null, null, false);

            // Frueher warfen beide schlicht eine NotImplementedException, obwohl sie ueber IPluginFactory
            // oeffentlich erreichbar sind.
            Assert.IsNotNull(scope.GetPlugin<CountingPlugin>(),
                "Ein Scope muss auch die Plugins der Factory sehen, auf der er geoeffnet wurde.");
            Assert.IsNull(scope[string.Empty],
                "Ein leerer Name ist schlicht 'kein Plugin' und darf keine ArgumentNullException werfen.");

            scope.Dispose();
        }

        private static string Ctor<T>()
        {
            // Vollen DLL-Pfad verwenden - FindAssemblyByFileName loest den Kurznamen des Testassemblies nicht auf.
            return $"[{typeof(T).Assembly.Location}]<{typeof(T).FullName}>";
        }
    }

    /// <summary>Ein triviales Plugin, das seine Dispose-Aufrufe zaehlt.</summary>
    public class CountingPlugin : IPlugin
    {
        public static int DisposeCount;

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose()
        {
            DisposeCount++;
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
