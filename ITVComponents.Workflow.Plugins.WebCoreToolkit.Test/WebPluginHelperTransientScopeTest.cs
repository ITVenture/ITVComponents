using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit.Test
{
    /// <summary>
    /// Regression fuer den Transient-Scope-Fix im <see cref="WebPluginHelper"/>: Wird ein Plugin INNERHALB
    /// eines expliziten Operations-Scopes aufgeloest (wie beim FreshInjectablePlugin-Lease oder im
    /// WebPluginActivityCatalog), muss eine dabei gezogene TRANSIENTE Sub-Abhaengigkeit mit dem Scope
    /// disponiert werden - nicht erst beim <c>ResetFactory()</c>. Ein NICHT-transientes (Factory-lebenslanges)
    /// Plugin darf beim Scope-Close NICHT disponiert werden (Gegenprobe).
    /// </summary>
    [TestClass]
    public class WebPluginHelperTransientScopeTest
    {
        [TestInitialize]
        public void Reset()
        {
            TransientSubPlugin.DisposeCount = 0;
            SingletonSubPlugin.DisposeCount = 0;
        }

        [TestMethod]
        public void TransientDependency_IsDisposedWithOperationScope()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: true);

            using (IPluginFactory scope = helper.CreateOperationScope("s"))
            {
                var host = scope["host", true] as HostPlugin;
                Assert.IsNotNull(host, "Host-Plugin konnte nicht aufgeloest werden.");
                Assert.IsNotNull(host.Sub, "Transiente Sub-Abhaengigkeit wurde nicht injiziert.");
                Assert.AreEqual(0, TransientSubPlugin.DisposeCount,
                    "Die Sub-Abhaengigkeit darf VOR dem Scope-Close nicht disponiert sein.");
            }

            Assert.AreEqual(1, TransientSubPlugin.DisposeCount,
                "Die transiente Sub-Abhaengigkeit muss mit dem Operations-Scope disponiert werden (nicht erst beim ResetFactory).");
        }

        [TestMethod]
        public void TransientPlugin_ResolvedDirectlyInScope_IsDisposedWithScope()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: true);

            using (IPluginFactory scope = helper.CreateOperationScope("s"))
            {
                var t = scope["sub", true] as TransientSubPlugin;
                Assert.IsNotNull(t, "Das transiente Plugin konnte nicht direkt aufgeloest werden.");
                Assert.AreEqual(0, TransientSubPlugin.DisposeCount,
                    "Das transiente Plugin darf VOR dem Scope-Close nicht disponiert sein.");
            }

            // Mit korrektem Gate wird das transient markierte Plugin in den aktiven Operations-Scope geladen und
            // faellt mit ihm. OHNE Gate wuerde der Handler den transienten Ladescope oeffnen und der Load-in-Scope
            // eine "There already is a plugin-load in progress"-Exception werfen (Beweis, dass der Transient-Modus
            // jetzt tatsaechlich zieht).
            Assert.AreEqual(1, TransientSubPlugin.DisposeCount,
                "Das direkt aufgeloeste transiente Plugin muss mit dem Operations-Scope disponiert werden.");
        }

        [TestMethod]
        public void NonTransientDependency_InsideScope_IsAlsoOwnedByScope()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: false);

            using (IPluginFactory scope = helper.CreateOperationScope("s"))
            {
                var host = scope["host", true] as HostPlugin;
                Assert.IsNotNull(host, "Host-Plugin konnte nicht aufgeloest werden.");
                Assert.IsNotNull(host.Sub, "Sub-Abhaengigkeit wurde nicht injiziert.");
                Assert.AreEqual(0, SingletonSubPlugin.DisposeCount,
                    "Die Sub-Abhaengigkeit darf VOR dem Scope-Close nicht disponiert sein.");
            }

            // Fresh-Garantie: Ein EXPLIZITER Operations-Scope besitzt ALLES, was in ihm aufgeloest wird - auch
            // NICHT-transiente Plugins. Die Factory darf den expliziten Scope nicht umgehen (frueher lief ein
            // nicht-transientes Plugin an ihm vorbei in pluginInstances). Also faellt auch dieses mit dem Scope.
            Assert.AreEqual(1, SingletonSubPlugin.DisposeCount,
                "Ein im expliziten Scope aufgeloestes Plugin muss mit dem Scope disponiert werden - auch wenn es nicht transient ist.");
        }

        /// <summary>
        /// Der SCOPE-FREIE Weg: Ein Plugin muss mit SEINEM EIGENEN Transient-Flag eingeordnet werden, nicht mit
        /// dem seiner zuletzt aufgeloesten Abhaengigkeit. <c>ParseConstructor</c> loest alle Ctor-Parameter auf,
        /// BEVOR das aeussere Plugin registriert wird - eine Zuweisung an <c>UseTransientScope</c> statt des
        /// <c>TransientLoad</c>-Tokens laesst die Registrierung des Hosts (Transient=false) das <c>true</c> der
        /// Sub-Abhaengigkeit sehen, der Host landet im transienten Ladescope und faellt mit ihm.
        /// </summary>
        [TestMethod]
        public void PluginIsRegisteredWithItsOwnTransientFlag_NotWithTheOneOfItsLastDependency()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: true);
            // Ueber den expliziten Plugin-Scope, wie die anderen Tests: nur dieser Weg laedt ohne
            // Security-Pruefung. Es wird KEIN Operations-Scope geoeffnet - der Load laeuft scope-frei, also
            // ueber den transienten Ladescope, den der Handler selbst aufmacht.
            PluginFactory factory = helper.GetFactory("s");

            var host = factory["host", true] as HostPlugin;
            Assert.IsNotNull(host, "Host-Plugin konnte nicht aufgeloest werden.");
            Assert.IsNotNull(host.Sub, "Transiente Sub-Abhaengigkeit wurde nicht injiziert.");

            // Host ist NICHT transient -> er gehoert der Factory und ist nach dem Ladevorgang weiter gebuffert.
            // Mit der Zuweisung statt des Tokens wird diese Assertion rot.
            Assert.IsNotNull(factory["host"],
                "Das nicht-transiente Host-Plugin muss den transienten Ladescope ueberleben (es wurde mit dem Transient-Flag seiner Abhaengigkeit registriert).");

            // Die Sub-Abhaengigkeit IST transient -> sie gehoert dem Ladescope und ist nicht mehr gebuffert.
            Assert.IsNull(factory["sub"],
                "Die transiente Sub-Abhaengigkeit darf nach dem Schliessen des Ladescopes nicht mehr in der Factory liegen.");
        }

        private static WebPluginHelper BuildHelper(bool transientSub)
        {
            // Vollen DLL-Pfad verwenden: WebPluginHelper baut seine Factory selbst (kein RegisterAssembly-Zugriff),
            // und FindAssemblyByFileName loest den Pfad auf (Identitaet bleibt via LoadFrom-Cache erhalten).
            string asm = typeof(HostPlugin).Assembly.Location;
            string subType = transientSub
                ? typeof(TransientSubPlugin).FullName
                : typeof(SingletonSubPlugin).FullName;

            var selector = new FakeSelector(new Dictionary<string, WebPlugin>
            {
                ["host"] = new WebPlugin
                {
                    UniqueName = "host",
                    Constructor = $"[{asm}]<{typeof(HostPlugin).FullName}>$sub"
                },
                ["sub"] = new WebPlugin
                {
                    UniqueName = "sub",
                    Constructor = $"[{asm}]<{subType}>",
                    Transient = transientSub
                }
            });

            IServiceProvider services = new ServiceCollection().BuildServiceProvider();
            return new WebPluginHelper(selector, services, Options.Create(new PluginsInitOptions()),
                new TestPermissionScope(), NullLogger<WebPluginHelper>.Instance);
        }
    }

    /// <summary>Ein triviales Plugin, das im Ctor eine (per $-Referenz benannte) Sub-Abhaengigkeit zieht.</summary>
    public class HostPlugin : IPlugin
    {
        public HostPlugin(IPlugin sub)
        {
            Sub = sub;
        }

        public IPlugin Sub { get; }

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Die TRANSIENTE Sub-Abhaengigkeit; zaehlt ihre Dispose-Aufrufe.</summary>
    public class TransientSubPlugin : IPlugin
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

    /// <summary>Die NICHT-transiente Sub-Abhaengigkeit (Gegenprobe); zaehlt ihre Dispose-Aufrufe.</summary>
    public class SingletonSubPlugin : IPlugin
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

    /// <summary>Ein In-Memory-<see cref="IWebPluginsSelector"/>, der den expliziten Scope-Pfad erlaubt.</summary>
    internal sealed class FakeSelector : IWebPluginsSelector
    {
        private readonly Dictionary<string, WebPlugin> plugins;

        public FakeSelector(Dictionary<string, WebPlugin> plugins)
        {
            this.plugins = plugins;
        }

        // protected internal set der Schnittstelle -> in fremder Assembly explizit implementieren.
        string IWebPluginsSelector.ExplicitPluginPermissionScope { get; set; }

        public bool ExplicitScopeSupported => true;

        public IEnumerable<WebPlugin> GetStartupPlugins() => Array.Empty<WebPlugin>();

        public WebPlugin GetPlugin(string uniqueName)
            => plugins.TryGetValue(uniqueName, out WebPlugin p) ? p : null;

        public IEnumerable<WebPlugin> GetAutoLoadPlugins() => Array.Empty<WebPlugin>();

        public void ConfigurePlugin(WebPlugin pi)
        {
        }

        public IEnumerable<WebPluginGenericParam> GetGenericParameters(string uniqueName)
            => Array.Empty<WebPluginGenericParam>();
    }
}
