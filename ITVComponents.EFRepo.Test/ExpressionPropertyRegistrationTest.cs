using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.EFRepo.Test
{
    /// <summary>
    /// Was <see cref="DbContextModelBuilderOptions{TContext}.ConfigureExpressionProperty{T}"/> annimmt -
    /// und was es zurueckweist.
    /// </summary>
    /// <remarks>
    /// Hintergrund: das Options-Objekt lebt in Produktion als Plugin einmal im Prozess, je Name gewinnt
    /// die erste Registrierung, und registriert wird ein Zugriff auf eine <b>Konstante</b>. Das geht nur
    /// deshalb gut, weil EF Core in einem Query-Filter eine <c>DbContext</c>-Konstante durch den gerade
    /// laufenden Kontext ersetzt. Fuer alles andere gilt diese Zusage nicht - und ohne die Pruefung
    /// waere daran nichts zu bemerken: es uebersetzt sauber und liefert Ergebnisse, nur eben die des
    /// ersten Registrierenden.
    /// </remarks>
    [TestClass]
    public class ExpressionPropertyRegistrationTest
    {
        [TestMethod]
        public void ContextBoundPropertyIsAccepted()
        {
            var options = new DbContextModelBuilderOptions<TestContext>();
            using var ctx = new TestContext();

            ctx.Register(options);
        }

        [TestMethod]
        public void StaticPropertyIsAccepted()
        {
            var options = new DbContextModelBuilderOptions<TestContext>();

            // Statisch heisst: keine Instanz, an der etwas haengenbleiben koennte.
            options.ConfigureExpressionProperty(() => Ambient.Tenant);
        }

        /// <summary>Der Fall, um den es geht: der Wert haengt an einem gefangenen Objekt.</summary>
        [TestMethod]
        public void ServiceBoundPropertyIsRejected()
        {
            var options = new DbContextModelBuilderOptions<TestContext>();
            var service = new TenantService { Tenant = "acme" };

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => options.ConfigureExpressionProperty(() => service.Tenant));

            StringAssert.Contains(ex.Message, "not rooted in the DbContext",
                "the message must say what is wrong, not just that something is");
        }

        /// <summary>
        /// Erneute Registrierung derselben Eigenschaft ist der Normalfall - ein neuer Kontext laeuft
        /// durch dieselbe Konfiguration. Sie muss durchgehen und darf nichts veraendern.
        /// </summary>
        [TestMethod]
        public void SameMemberMayBeRegisteredAgain()
        {
            var options = new DbContextModelBuilderOptions<TestContext>();
            using var first = new TestContext();
            using var second = new TestContext();

            first.Register(options);
            second.Register(options);
        }

        /// <summary>Ein ANDERES Member unter demselben Namen waere bisher still verworfen worden.</summary>
        [TestMethod]
        public void DifferentMemberUnderTheSameNameIsRejected()
        {
            var options = new DbContextModelBuilderOptions<TestContext>();
            using var ctx = new TestContext();

            ctx.Register(options);

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => ctx.RegisterImpostor(options));
            StringAssert.Contains(ex.Message, "already registered");
        }

        /// <summary>
        /// Viele Kontexte, die im selben Moment entstehen, registrieren dieselbe Eigenschaft - und keiner
        /// darf daran scheitern.
        /// </summary>
        /// <remarks>
        /// Der Anlass ist ein realer Fehler aus dem Betrieb: <i>An item with the same key has already been
        /// added. Key: CurrentTenant</i>, geworfen beim Aufbau eines Sicherheits-Kontextes. Die Pruefung
        /// "kenne ich den Namen schon?" und das Anlegen waren zwei Schritte; zwei gleichzeitig entstehende
        /// Kontexte sahen den Namen beide als unbekannt und legten ihn beide an. Das Options-Objekt lebt in
        /// Produktion einmal im Prozess, Kontexte entstehen dauernd - unter Last war das eine Frage der
        /// Zeit, und es traf ausgerechnet den Aufbau des Kontextes.
        /// <para>
        /// Der Test kann den Wettlauf nicht erzwingen; er macht ihn wahrscheinlich. Er schlaegt deshalb
        /// nicht bei jedem Lauf fehl, wenn die Entdopplung wieder in zwei Schritte zerfaellt - aber er
        /// schlaegt nie fehl, solange sie einer ist.
        /// </para>
        /// </remarks>
        [TestMethod]
        public void ManyContextsMayRegisterTheSamePropertyAtOnce()
        {
            var options = new DbContextModelBuilderOptions<TestContext>();
            var contexts = Enumerable.Range(0, 64).Select(_ => new TestContext()).ToArray();
            var failures = new ConcurrentBag<Exception>();

            try
            {
                // Alle gleichzeitig loslassen: ohne die Barriere sind die ersten laengst fertig, bevor die
                // letzten anfangen, und genau das Fenster, um das es geht, waere nie offen.
                using var gate = new Barrier(contexts.Length);
                Parallel.ForEach(contexts, ctx =>
                {
                    gate.SignalAndWait();
                    try
                    {
                        ctx.Register(options);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex);
                    }
                });

                Assert.AreEqual(0, failures.Count,
                    "Gleichzeitiges Registrieren desselben Namens muss durchgehen - der erste gewinnt, die "
                    + "uebrigen sind ein Nichts-Tun. Erster Fehler: " + failures.FirstOrDefault()?.Message);
            }
            finally
            {
                foreach (var ctx in contexts)
                {
                    ctx.Dispose();
                }
            }
        }

        private sealed class TenantService
        {
            public string Tenant { get; set; }
        }

        private static class Ambient
        {
            [ExpressionPropertyRedirect("Tenant")]
            public static string Tenant => "";
        }

        private sealed class TestContext : DbContext
        {
            [ExpressionPropertyRedirect("Tenant")]
            public string CurrentTenant => "acme";

            /// <summary>Zweite Eigenschaft unter DEMSELBEN Platzhalter-Namen.</summary>
            [ExpressionPropertyRedirect("Tenant")]
            public string OtherTenant => "beta";

            /// <summary>
            /// Registriert von INNEN - so, wie es die Kontexte im Toolkit in ihrem OnModelCreating tun.
            /// Der Ausdruck steht dadurch auf der Kontext-Instanz.
            /// </summary>
            public void Register(DbContextModelBuilderOptions<TestContext> options)
                => options.ConfigureExpressionProperty(() => CurrentTenant);

            public void RegisterImpostor(DbContextModelBuilderOptions<TestContext> options)
                => options.ConfigureExpressionProperty(() => OtherTenant);

            // Bewusst ohne OnConfiguring: hier wird nur registriert, nie abgefragt - der Kontext ist
            // Traeger der Eigenschaften und braucht keine Datenbank.
        }
    }
}
