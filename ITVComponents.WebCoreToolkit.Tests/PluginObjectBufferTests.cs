using System;
using ITVComponents.DIServices;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Der Puffer der Plugin-Fabrik (Settings, Plugin-Zeilen, generische Parameter, Konstanten) je Mandant.
    /// </summary>
    /// <remarks>
    /// Frueher war er ohne <c>IMemoryCache</c> still abgeschaltet, und jede Aufloesung eines Fachkontexts ging
    /// mit gut hundert Abfragen in die Datenbank. Jeder Test nimmt einen eigenen Mandanten-Namen, damit kein
    /// Test vom Stand eines anderen abhaengt.
    /// </remarks>
    [TestClass]
    public class PluginObjectBufferTests
    {
        private static string NewScope() => $"Scope{Guid.NewGuid():N}";

        private static IServiceProvider Services(bool memoryCache = false, IEntityChangeSignal signal = null,
            bool pluginSystem = true)
        {
            var services = new ServiceCollection();
            if (pluginSystem)
            {
                services.InitializePluginSystem();
            }

            if (memoryCache)
            {
                services.AddMemoryCache();
            }

            if (signal != null)
            {
                services.AddSingleton(signal);
            }

            return services.BuildServiceProvider();
        }

        [TestMethod]
        public void WithoutMemoryCache_TheBufferWorksAcrossScopes()
        {
            string scope = NewScope();
            var root = Services();
            int reads = 0;

            // Zwei DI-Scopes stehen fuer zwei Anfragen bzw. zwei Kacheln.
            using var a = root.CreateScope();
            using var b = root.CreateScope();
            IObjectProvider first = a.ServiceProvider.GetObjectProvider(scope);
            IObjectProvider second = b.ServiceProvider.GetObjectProvider(scope);

            Assert.AreSame(first, second);
            Assert.IsNull(first.GetBufferedValue<string>("PreInitSequenceForX", _ => { reads++; return null; }, null));
            Assert.IsNull(second.GetBufferedValue<string>("PreInitSequenceForX", _ => { reads++; return null; }, null));
            Assert.AreEqual(1, reads, "auch 'gibt es nicht' muss gepuffert sein");
        }

        /// <summary>
        /// Zwei Container in einem Prozess (Integrationstests, mehrere Hosts) definieren unter demselben
        /// Mandanten-Namen womoeglich verschiedene Plugins. Sie duerfen sich den Puffer nicht teilen.
        /// </summary>
        [TestMethod]
        public void TwoContainers_DoNotShareTheBuffer()
        {
            string scope = NewScope();

            Services().GetObjectProvider(scope).GetBufferedValue("TenantPI#ctx", _ => "erster Container", null);
            string second = Services().GetObjectProvider(scope).GetBufferedValue("TenantPI#ctx", _ => "zweiter Container", null);

            Assert.AreEqual("zweiter Container", second);
        }

        /// <summary>
        /// Wer die Plugin-Fabrik von Hand aufbaut (ohne <c>InitializePluginSystem</c>) und keinen Memory-Cache hat,
        /// bekommt wie bisher keinen Puffer. Das steht dann als Warnung im Log.
        /// </summary>
        [TestMethod]
        public void WithoutPluginSystemAndMemoryCache_NothingIsBuffered()
        {
            string scope = NewScope();
            var services = Services(pluginSystem: false);
            int reads = 0;

            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);
            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);

            Assert.AreEqual(2, reads);
        }

        /// <summary>
        /// MVC bringt einen Memory-Cache mit, Blazor nicht. Der Puffer liegt trotzdem in beiden Faellen am selben
        /// Ort, damit sich beide Welten gleich verhalten.
        /// </summary>
        [TestMethod]
        public void WithMemoryCache_TheBufferStaysInTheContainerStore()
        {
            string scope = NewScope();
            var services = Services(memoryCache: true);
            int reads = 0;

            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);
            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);

            Assert.AreEqual(1, reads);
            Assert.IsFalse(services.GetRequiredService<IMemoryCache>().TryGetValue($"{scope}_WPHObjects", out _),
                "der Memory-Cache ist nur der Notnagel");
        }

        /// <summary>Eine Fabrik ohne <c>InitializePluginSystem</c> puffert im Memory-Cache, wenn es einen gibt.</summary>
        [TestMethod]
        public void WithoutPluginSystem_TheMemoryCacheIsTheFallback()
        {
            string scope = NewScope();
            var services = Services(memoryCache: true, pluginSystem: false);
            int reads = 0;

            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);
            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);

            Assert.AreEqual(1, reads);
            Assert.IsTrue(services.GetRequiredService<IMemoryCache>().TryGetValue($"{scope}_WPHObjects", out _));
        }

        [TestMethod]
        public void DifferentScopes_HaveDifferentBuffers()
        {
            var services = Services();

            services.GetObjectProvider(NewScope()).GetBufferedValue("k", _ => "A", null);
            string value = services.GetObjectProvider(NewScope()).GetBufferedValue("k", _ => "B", null);

            Assert.AreEqual("B", value);
        }

        [TestMethod]
        public void AChangeOfThePluginConfiguration_ReadsAgain()
        {
            string scope = NewScope();
            var signal = new ManualSignal();
            var services = Services(signal: signal);
            int reads = 0;

            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);
            signal.Touch(EntityChangeTopics.PluginConfiguration);
            int afterChange = services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);
            int thereafter = services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);

            Assert.AreEqual(2, afterChange, "nach der Aenderung wird neu gelesen");
            Assert.AreEqual(2, thereafter, "danach wieder aus dem Puffer");
        }

        [TestMethod]
        public void AChangeOfAnotherTopic_KeepsTheBuffer()
        {
            string scope = NewScope();
            var signal = new ManualSignal();
            var services = Services(signal: signal);
            int reads = 0;

            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);
            signal.Touch(EntityChangeTopics.Navigation);
            services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);

            Assert.AreEqual(1, reads);
        }

        /// <summary>
        /// Wer liest, waehrend geschrieben wird, hat womoeglich den alten Stand. Der Eintrag muss deshalb beim
        /// naechsten Zugriff neu gelesen werden, auch wenn er erst nach der Aenderung im Puffer gelandet ist.
        /// </summary>
        [TestMethod]
        public void AChangeDuringReading_ReadsAgainNextTime()
        {
            string scope = NewScope();
            var signal = new ManualSignal();
            var services = Services(signal: signal);
            int reads = 0;

            services.GetObjectProvider(scope).GetBufferedValue("k", _ =>
            {
                reads++;
                signal.Touch(EntityChangeTopics.PluginConfiguration);
                return reads;
            }, null);
            int next = services.GetObjectProvider(scope).GetBufferedValue("k", _ => ++reads, null);

            Assert.AreEqual(2, next);
        }

        /// <summary>
        /// Die Cookie-Ablage behaelt ihr Verhalten: ohne Memory-Cache gibt es keine serverseitigen Cookies.
        /// </summary>
        [TestMethod]
        public void TheNamedOverload_WithoutMemoryCache_StillReturnsTheDefault()
        {
            Assert.IsNull(Services().GetObjectProvider("ITVWCT:DCS:Buffer", null));
        }

        private sealed class ManualSignal : IEntityChangeSignal
        {
            private DateTime pluginConfiguration = DateTime.MinValue;
            private DateTime navigation = DateTime.MinValue;

            public event Action<string> Changed;

            public void Touch(string topic)
            {
                // Die Uhr muss vor und nach dem Stempel weiterlaufen, sonst sind "vorher gelesen" und "nachher
                // gelesen" bei grober Aufloesung nicht zu unterscheiden.
                WaitForClock();
                var now = DateTime.UtcNow;
                WaitForClock();
                if (topic == EntityChangeTopics.PluginConfiguration)
                {
                    pluginConfiguration = now;
                }
                else
                {
                    navigation = now;
                }

                Changed?.Invoke(topic);
            }

            private static void WaitForClock()
            {
                var start = DateTime.UtcNow;
                while (DateTime.UtcNow <= start)
                {
                }
            }

            public DateTime GetLastChange(string topic)
                => topic == EntityChangeTopics.PluginConfiguration ? pluginConfiguration
                    : topic == EntityChangeTopics.Navigation ? navigation
                    : DateTime.MinValue;
        }
    }
}
