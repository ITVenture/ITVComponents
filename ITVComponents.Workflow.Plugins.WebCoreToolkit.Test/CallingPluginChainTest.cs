using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Configuration;
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
    /// Diagnose fuer die CallingPlugin-Kette: Ein generisches Blatt-Plugin leitet sein Typ-Argument aus
    /// <c>CallingPlugin.PluginType</c> ab. Erwartung: das ist IMMER der unmittelbare Vorgaenger - also der
    /// Kontext, der das Blatt anfordert, unabhaengig davon, wer den Kontext angefordert hat.
    /// </summary>
    [TestClass]
    public class CallingPluginChainTest
    {
        [TestInitialize]
        public void Reset()
        {
            CallingPluginProbe.LeafTypes.Clear();
            CallingPluginProbe.OptionTypes.Clear();
        }

        [TestMethod]
        public void Depth2_LeafSeesTheContext()
        {
            using WebPluginHelper helper = BuildHelper();
            // Expliziter Scope -> checkSecurity=false (wie im Transient-Scope-Test); ohne echten
            // Security-Unterbau lief die Permission-Pruefung sonst in eine NRE.
            PluginFactory factory = helper.GetFactory("s");

            var ctx = factory["ctx", true] as CtxPlugin;

            Assert.IsNotNull(ctx, "ctx konnte nicht aufgeloest werden.");
            CollectionAssert.AreEqual(new[] { typeof(CtxPlugin) }, CallingPluginProbe.LeafTypes,
                $"Tiefe 2: erwartet CtxPlugin, gesehen: {string.Join(", ", CallingPluginProbe.LeafTypes)}");
        }

        [TestMethod]
        public void Depth3_LeafStillSeesTheContext_NotTheHandler()
        {
            using WebPluginHelper helper = BuildHelper();
            PluginFactory factory = helper.GetFactory("s");

            var handler = factory["handler", true] as HandlerPlugin;

            Assert.IsNotNull(handler, "handler konnte nicht aufgeloest werden.");
            CollectionAssert.AreEqual(new[] { typeof(CtxPlugin) }, CallingPluginProbe.LeafTypes,
                $"Tiefe 3: erwartet CtxPlugin (unmittelbarer Vorgaenger), gesehen: {string.Join(", ", CallingPluginProbe.LeafTypes)}");
        }

        /// <summary>
        /// Dieselbe Kette, aber das Blatt haengt an der PreInit-Sequenz DES KONTEXTS statt an dessen
        /// Konstruktor. Konzeptionell ist der Kontext der Anforderer - die Sequenz gehoert ihm. Der Handler
        /// darf hier nicht durchschlagen.
        /// </summary>
        [TestMethod]
        public void Depth3_PreInitSequenceOfTheContext_MustNotSeeTheHandler()
        {
            using WebPluginHelper helper = BuildHelper(preInitOfCtx: "[\"leaf\"]");
            PluginFactory factory = helper.GetFactory("s");

            var handler = factory["handler", true] as HandlerPlugin;

            Assert.IsNotNull(handler, "handler konnte nicht aufgeloest werden.");
            CollectionAssert.AreEqual(new[] { typeof(CtxPlugin) }, CallingPluginProbe.LeafTypes,
                $"PreInit-Sequenz von ctx: erwartet CtxPlugin, gesehen: {string.Join(", ", CallingPluginProbe.LeafTypes)}");
        }

        /// <summary>
        /// Die Kette selbst: aus dem Blatt heraus muss ueber <c>PrevPlugin(1)</c> der Vor-Vorgaenger
        /// erreichbar sein - also der Handler, der den Kontext verlangt hat.
        /// </summary>
        [TestMethod]
        public void PrevPlugin_ReachesTheStepBeyondTheImmediateCaller()
        {
            using WebPluginHelper helper = BuildHelper(leafTypeExpression: "CallingPlugin.PrevPlugin(1).PluginType");
            PluginFactory factory = helper.GetFactory("s");

            var handler = factory["handler", true] as HandlerPlugin;

            Assert.IsNotNull(handler, "handler konnte nicht aufgeloest werden.");
            CollectionAssert.AreEqual(new[] { typeof(HandlerPlugin) }, CallingPluginProbe.LeafTypes,
                $"PrevPlugin(1): erwartet HandlerPlugin, gesehen: {string.Join(", ", CallingPluginProbe.LeafTypes)}");
        }

        private static WebPluginHelper BuildHelper(string preInitOfCtx = null,
            string leafTypeExpression = "CallingPlugin.PluginType")
        {
            string asm = typeof(CtxPlugin).Assembly.Location;

            var selector = new GenericFakeSelector(
                new Dictionary<string, WebPlugin>
                {
                    ["handler"] = new WebPlugin
                    {
                        UniqueName = "handler",
                        Constructor = $"[{asm}]<{typeof(HandlerPlugin).FullName}>$ctx"
                    },
                    ["ctx"] = new WebPlugin
                    {
                        UniqueName = "ctx",
                        // Mit PreInit-Sequenz zieht der Kontext das Blatt ueber die Sequenz statt ueber den Ctor.
                        Constructor = preInitOfCtx == null
                            ? $"[{asm}]<{typeof(CtxPlugin).FullName}>$leaf"
                            : $"[{asm}]<{typeof(CtxPlugin).FullName}>"
                    },
                    ["leaf"] = new WebPlugin
                    {
                        UniqueName = "leaf",
                        Constructor = $"[{asm}]<{typeof(LeafPlugin<>).FullName}>"
                    }
                },
                new Dictionary<string, WebPluginGenericParam[]>
                {
                    ["leaf"] = new[]
                    {
                        new WebPluginGenericParam
                        {
                            GenericTypeName = "T",
                            TypeExpression = leafTypeExpression
                        }
                    }
                });

            var sc = new ServiceCollection();
            if (preInitOfCtx != null)
            {
                sc.AddSingleton<IGlobalSettingsProvider>(
                    new FixedSettingsProvider("PreInitSequenceForctx", preInitOfCtx));
            }

            IServiceProvider services = sc.BuildServiceProvider();
            return new WebPluginHelper(selector, services, Options.Create(new PluginsInitOptions()),
                new TestPermissionScope(), NullLogger<WebPluginHelper>.Instance);
        }

        /// <summary>
        /// Die echte Form mit vier Stufen: handler -> ctx -> collector -> (PreInit-Sequenz) -> option.
        /// Von der Option aus muss <c>CallingPlugin.PrevPlugin(n)</c> den Baum stufenweise rueckwaerts
        /// laufen: 0 = collector, 1 = ctx, 2 = handler. Damit ist es fuer die Option egal, wie tief die
        /// Kette darueber noch weitergeht - "den Kontext" erreicht sie immer mit derselben Zahl.
        /// </summary>
        [TestMethod]
        public void PrevPlugin_WalksTheTreeBackwards_FourLevels()
        {
            using WebPluginHelper helper = BuildFourLevelHelper();
            PluginFactory factory = helper.GetFactory("s");

            var handler = factory["handler", true] as HandlerPlugin;

            Assert.IsNotNull(handler, "handler konnte nicht aufgeloest werden.");
            CollectionAssert.AreEqual(
                new[] { typeof(CollectorPlugin), typeof(CtxPlugin), typeof(HandlerPlugin) },
                CallingPluginProbe.OptionTypes,
                "erwartet Collector/Ctx/Handler fuer PrevPlugin(0)/(1)/(2), gesehen: "
                + string.Join(", ", CallingPluginProbe.OptionTypes));
        }

        private static WebPluginHelper BuildFourLevelHelper()
        {
            string asm = typeof(CtxPlugin).Assembly.Location;

            var selector = new GenericFakeSelector(
                new Dictionary<string, WebPlugin>
                {
                    ["handler"] = new WebPlugin
                    {
                        UniqueName = "handler",
                        Constructor = $"[{asm}]<{typeof(HandlerPlugin).FullName}>$ctx"
                    },
                    ["ctx"] = new WebPlugin
                    {
                        UniqueName = "ctx",
                        Constructor = $"[{asm}]<{typeof(CtxPlugin).FullName}>$collector"
                    },
                    ["collector"] = new WebPlugin
                    {
                        UniqueName = "collector",
                        // Der Collector zieht die Option AUSSCHLIESSLICH ueber seine PreInit-Sequenz.
                        Constructor = $"[{asm}]<{typeof(CollectorPlugin).FullName}>"
                    },
                    ["option"] = new WebPlugin
                    {
                        UniqueName = "option",
                        Constructor = $"[{asm}]<{typeof(OptionPlugin<,,>).FullName}>"
                    }
                },
                new Dictionary<string, WebPluginGenericParam[]>
                {
                    ["option"] = new[]
                    {
                        new WebPluginGenericParam
                        {
                            GenericTypeName = "T0",
                            TypeExpression = "CallingPlugin.PrevPlugin(0).PluginType"
                        },
                        new WebPluginGenericParam
                        {
                            GenericTypeName = "T1",
                            TypeExpression = "CallingPlugin.PrevPlugin(1).PluginType"
                        },
                        new WebPluginGenericParam
                        {
                            GenericTypeName = "T2",
                            TypeExpression = "CallingPlugin.PrevPlugin(2).PluginType"
                        }
                    }
                });

            var sc = new ServiceCollection();
            sc.AddSingleton<IGlobalSettingsProvider>(
                new FixedSettingsProvider("PreInitSequenceForcollector", "[\"option\"]"));

            IServiceProvider services = sc.BuildServiceProvider();
            return new WebPluginHelper(selector, services, Options.Create(new PluginsInitOptions()),
                new TestPermissionScope(), NullLogger<WebPluginHelper>.Instance);
        }
    }

    /// <summary>Nicht-generischer Sammelpunkt (statische Felder generischer Klassen sind pro T).</summary>
    public static class CallingPluginProbe
    {
        public static readonly List<Type> LeafTypes = new();

        /// <summary>Die drei Stufen, die eine Option ueber PrevPlugin(0..2) gesehen hat.</summary>
        public static readonly List<Type> OptionTypes = new();
    }

    /// <summary>Der Options-Collector: zieht seine Optionen ueber die PreInit-Sequenz.</summary>
    public class CollectorPlugin : IPlugin
    {
        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Eine Option: haelt drei Stufen der Aufrufkette als Typ-Argumente fest.</summary>
    public class OptionPlugin<T0, T1, T2> : IPlugin
    {
        public OptionPlugin()
        {
            CallingPluginProbe.OptionTypes.Add(typeof(T0));
            CallingPluginProbe.OptionTypes.Add(typeof(T1));
            CallingPluginProbe.OptionTypes.Add(typeof(T2));
        }

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Das Blatt: haelt fest, welchen Typ es via CallingPlugin zugewiesen bekommen hat.</summary>
    public class LeafPlugin<T> : IPlugin
    {
        public LeafPlugin()
        {
            CallingPluginProbe.LeafTypes.Add(typeof(T));
        }

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Der "DbContext": verlangt das Blatt.</summary>
    public class CtxPlugin : IPlugin
    {
        public CtxPlugin()
        {
        }

        public CtxPlugin(IPlugin leaf)
        {
            Leaf = leaf;
        }

        public IPlugin Leaf { get; }

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Die zusaetzliche Stufe: verlangt den Kontext.</summary>
    public class HandlerPlugin : IPlugin
    {
        public HandlerPlugin(IPlugin ctx)
        {
            Ctx = ctx;
        }

        public IPlugin Ctx { get; }

        public string UniqueName { get; set; }

        public event EventHandler Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Liefert genau EINEN Schluessel - fuer die PreInit-Sequenz.</summary>
    internal sealed class FixedSettingsProvider : IGlobalSettingsProvider
    {
        private readonly string key;
        private readonly string value;

        public FixedSettingsProvider(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        public string GetJsonSetting(string key) => key == this.key ? value : null;

        public string GetLiteralSetting(string key) => null;
    }

    /// <summary>Wie FakeSelector, aber mit konfigurierbaren generischen Parametern.</summary>
    internal sealed class GenericFakeSelector : IWebPluginsSelector
    {
        private readonly Dictionary<string, WebPlugin> plugins;
        private readonly Dictionary<string, WebPluginGenericParam[]> genericParams;

        public GenericFakeSelector(Dictionary<string, WebPlugin> plugins,
            Dictionary<string, WebPluginGenericParam[]> genericParams)
        {
            this.plugins = plugins;
            this.genericParams = genericParams;
        }

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
            => genericParams.TryGetValue(uniqueName, out WebPluginGenericParam[] p)
                ? p
                : Array.Empty<WebPluginGenericParam>();
    }
}
