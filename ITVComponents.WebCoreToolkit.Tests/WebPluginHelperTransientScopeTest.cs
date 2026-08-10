using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
// Der Testnamespace liegt unter ITVComponents.WebCoreToolkit, wo es einen Namespace 'Options' gibt - ohne Alias
// bindet 'Options.Create' dorthin statt an die statische Klasse.
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Regression fuer den Transient-Scope-Fix im <see cref="WebPluginHelper"/>. Drei Zusicherungen:
    /// wird ein Plugin INNERHALB eines expliziten Scopes aufgeloest, gehoert es diesem Scope (auch eine dabei
    /// gezogene transiente Sub-Abhaengigkeit, und auch dann, wenn es selbst nicht transient ist); und ausserhalb
    /// eines Scopes wird jedes Plugin mit SEINEM EIGENEN Transient-Flag eingeordnet - nicht mit dem seiner zuletzt
    /// aufgeloesten Abhaengigkeit.
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
        public void TransientDependency_IsDisposedWithTheExplicitScope()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: true);
            PluginFactory factory = helper.GetFactory("s");

            using (IPluginFactory scope = factory.NewScope(null, null, false))
            {
                var host = scope["host", true] as HostPlugin;
                Assert.IsNotNull(host, "Host-Plugin konnte nicht aufgeloest werden.");
                Assert.IsNotNull(host.Sub, "Transiente Sub-Abhaengigkeit wurde nicht injiziert.");
                Assert.AreEqual(0, TransientSubPlugin.DisposeCount,
                    "Die Sub-Abhaengigkeit darf VOR dem Scope-Close nicht disponiert sein.");
            }

            Assert.AreEqual(1, TransientSubPlugin.DisposeCount,
                "Die transiente Sub-Abhaengigkeit muss mit dem Scope disponiert werden - nicht erst beim ResetFactory.");
        }

        [TestMethod]
        public void TransientPlugin_ResolvedDirectlyInScope_IsDisposedWithScope()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: true);
            PluginFactory factory = helper.GetFactory("s");

            using (IPluginFactory scope = factory.NewScope(null, null, false))
            {
                var t = scope["sub", true] as TransientSubPlugin;
                Assert.IsNotNull(t, "Das transiente Plugin konnte nicht direkt aufgeloest werden.");
                Assert.AreEqual(0, TransientSubPlugin.DisposeCount,
                    "Das transiente Plugin darf VOR dem Scope-Close nicht disponiert sein.");
            }

            // Mit korrektem Gate wird das transient markierte Plugin in den aktiven Scope geladen und faellt mit ihm.
            // OHNE Gate wuerde der Handler zusaetzlich einen transienten Ladescope oeffnen und der Load-in-Scope eine
            // "There already is a plugin-load in progress"-Exception werfen.
            Assert.AreEqual(1, TransientSubPlugin.DisposeCount,
                "Das direkt aufgeloeste transiente Plugin muss mit dem Scope disponiert werden.");
        }

        [TestMethod]
        public void NonTransientDependency_InsideScope_IsAlsoOwnedByScope()
        {
            using WebPluginHelper helper = BuildHelper(transientSub: false);
            PluginFactory factory = helper.GetFactory("s");

            using (IPluginFactory scope = factory.NewScope(null, null, false))
            {
                var host = scope["host", true] as HostPlugin;
                Assert.IsNotNull(host, "Host-Plugin konnte nicht aufgeloest werden.");
                Assert.IsNotNull(host.Sub, "Sub-Abhaengigkeit wurde nicht injiziert.");
                Assert.AreEqual(0, SingletonSubPlugin.DisposeCount,
                    "Die Sub-Abhaengigkeit darf VOR dem Scope-Close nicht disponiert sein.");
            }

            // Ein EXPLIZITER Scope besitzt ALLES, was in ihm aufgeloest wird - auch nicht-transiente Plugins. Die
            // Factory darf ihn nicht umgehen (frueher lief ein nicht-transientes Plugin an ihm vorbei in die
            // factory-weite Sammlung und ueberlebte ihn).
            Assert.AreEqual(1, SingletonSubPlugin.DisposeCount,
                "Ein im expliziten Scope aufgeloestes Plugin muss mit dem Scope disponiert werden - auch wenn es nicht transient ist.");
        }

        [TestMethod]
        public void PluginIsRegisteredWithItsOwnTransientFlag_NotWithTheOneOfItsLastDependency()
        {
            // Scope-freier Weg: der Handler oeffnet einen transienten Ladescope. Das Host-Plugin ist NICHT transient
            // und muss die Aufloesungskette ueberleben; seine transiente Sub-Abhaengigkeit darf es nicht.
            // Ein einzelnes Flag auf der Factory wuerde hier kippen: die Sub-Abhaengigkeit wird VOR der
            // Registrierung des Hosts aufgeloest und setzte das Flag auf "transient" - der Host waere mit in den
            // Ladescope gerutscht und beim ResetFactory gestorben.
            using WebPluginHelper helper = BuildHelper(transientSub: true);
            PluginFactory factory = helper.GetFactory("s");

            var host = factory["host", true] as HostPlugin;

            Assert.IsNotNull(host, "Host-Plugin konnte nicht aufgeloest werden.");
            Assert.IsNotNull(host.Sub, "Transiente Sub-Abhaengigkeit wurde nicht injiziert.");
            Assert.IsNotNull(factory["host"],
                "Das nicht-transiente Host-Plugin muss in der factory-weiten Sammlung liegen und die Ladekette ueberleben.");
            Assert.IsNull(factory["sub"],
                "Die transiente Sub-Abhaengigkeit gehoert dem Ladescope und darf nicht in der factory-weiten Sammlung liegen.");

            helper.ResetFactory();

            Assert.AreEqual(1, TransientSubPlugin.DisposeCount,
                "Die transiente Sub-Abhaengigkeit muss beim ResetFactory disponiert werden.");
        }

        private static WebPluginHelper BuildHelper(bool transientSub)
        {
            // Vollen DLL-Pfad verwenden: der WebPluginHelper baut seine Factory selbst (kein RegisterAssembly-Zugriff)
            // und FindAssemblyByFileName loest den Kurznamen des Testassemblies nicht auf.
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
            return new WebPluginHelper(selector, services, MsOptions.Create(new PluginsInitOptions()),
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

    /// <summary>Ein IPermissionScope-Test-Doppel: kein HTTP-Scope, aber die Basisklasse traegt den Rest.</summary>
    public sealed class TestPermissionScope : PermissionScopeBase
    {
        protected override string GetPermissionScopePrefix() => null;

        protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
        {
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
