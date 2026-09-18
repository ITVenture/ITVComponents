using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Die Buchung eingehender Zahlungsmeldungen.
    /// </summary>
    /// <remarks>
    /// Geprüft wird hier nicht, ob ein Anbieter richtig gelesen wird — das braucht ein echtes Konto.
    /// Geprüft wird, was danach passiert, und das ist die Hälfte, bei der ein Fehler Geld kostet: eine
    /// zweimal gelieferte Meldung darf nicht zweimal freigeben, eine verspätete Absage keine Zahlung
    /// zurücknehmen, und eine gespiegelte Erstattung nicht doppelt zählen.
    /// </remarks>
    [TestClass]
    public class TenantSaleWebhookSinkTests
    {
        [TestMethod]
        public async Task MarkPaid_ReleasesOnceAndOnlyOnce()
        {
            var (sink, factory, observer) = Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await Seed(db, amountMinor: 5000);

                var first = await sink.MarkPaidAsync(sale, db, "charge-1", "buyer@example.com", default);
                Assert.IsTrue(first, "The first notification must release the sale.");

                var second = await sink.MarkPaidAsync(sale, db, "charge-1", "buyer@example.com", default);
                Assert.IsFalse(second, "A repeated delivery must not release the sale a second time.");
            }

            Assert.AreEqual(1, observer.Completed, "The observers must hear about the payment exactly once.");

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.SingleAsync();
            Assert.AreEqual(TenantSaleStatus.Paid, stored.Status);
            Assert.AreEqual("charge-1", stored.ProviderChargeId);
            Assert.AreEqual("buyer@example.com", stored.CustomerEmail);
            Assert.IsNull(stored.CheckoutUrl, "The spent payment page must not stay on the row — it could be paid again.");
        }

        [TestMethod]
        public async Task MarkPaid_StillRecordsTheChargeIdThatArrivesLate()
        {
            // Der Fall: die erste Meldung kennt die Kennung noch nicht, die zweite schon. Ohne sie laesst
            // sich spaeter nicht erstatten - sie muss also AUCH bei einer Wiederholung nachgetragen werden.
            var (sink, factory, _) = Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await Seed(db, amountMinor: 5000);
                await sink.MarkPaidAsync(sale, db, null, null, default);
                await sink.MarkPaidAsync(sale, db, "charge-late", null, default);
            }

            await using var check = await factory.CreateDbContextAsync();
            Assert.AreEqual("charge-late", (await check.TenantSales.SingleAsync()).ProviderChargeId);
        }

        [TestMethod]
        public async Task MoveTo_LeavesAPaidSaleAlone()
        {
            // Meldungen ueberholen sich. Eine verspaetete Absage darf eine Zahlung nicht zuruecknehmen.
            var (sink, factory, _) = Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await Seed(db, amountMinor: 5000);
                await sink.MarkPaidAsync(sale, db, "charge-1", null, default);
                await sink.MoveToAsync(sale, db, TenantSaleStatus.Canceled, default);
            }

            await using var check = await factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Paid, (await check.TenantSales.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task MirrorRefund_CountsTheSameRefundOnlyOnce()
        {
            var (sink, factory, observer) = Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await Seed(db, amountMinor: 5000);
                await sink.MarkPaidAsync(sale, db, "charge-1", null, default);
                await sink.MirrorRefundAsync(sale, db, "refund-1", 2000, "SUCCESSFUL", default);
                await sink.MirrorRefundAsync(sale, db, "refund-1", 2000, "SUCCESSFUL", default);
            }

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.Include(s => s.Refunds).SingleAsync();
            Assert.AreEqual(1, stored.Refunds.Count);
            Assert.AreEqual(TenantSaleStatus.PartiallyRefunded, stored.Status);
            Assert.AreEqual(1, observer.Refunded);
        }

        [TestMethod]
        public async Task MirrorRefundTotal_BooksOnlyTheDifference()
        {
            // Der Payrexx-Fall: gemeldet wird ein Gesamtstand, nicht eine einzelne Erstattung.
            var (sink, factory, _) = Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await Seed(db, amountMinor: 5000);
                await sink.MarkPaidAsync(sale, db, "charge-1", null, default);

                await sink.MirrorRefundTotalAsync(sale, db, 2000, "payrexx", "partially-refunded", default);
                // Dieselbe Meldung nochmals: derselbe Gesamtstand ergibt dieselbe Kennung und faellt weg.
                await sink.MirrorRefundTotalAsync(sale, db, 2000, "payrexx", "partially-refunded", default);
                // Eine zweite, ECHTE Teilerstattung: anderer Gesamtstand, also eine neue Zeile ueber die
                // Differenz - nicht ueber die 5000.
                await sink.MirrorRefundTotalAsync(sale, db, 5000, "payrexx", "refunded", default);
            }

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.Include(s => s.Refunds).SingleAsync();
            Assert.AreEqual(2, stored.Refunds.Count, "The repeated total must not produce a third row.");
            Assert.AreEqual(5000, stored.Refunds.Sum(r => r.AmountMinor));
            Assert.AreEqual(TenantSaleStatus.Refunded, stored.Status);
        }

        [TestMethod]
        public async Task MirrorRefundTotal_NeverTakesARefundBack()
        {
            // Meldet der Anbieter WENIGER als lokal gebucht ist, wird nichts geaendert. Eine Erstattung
            // zurueckzunehmen, weil eine Zahl kleiner ist als erwartet, waere schlimmer als die Abweichung.
            var (sink, factory, _) = Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await Seed(db, amountMinor: 5000);
                await sink.MarkPaidAsync(sale, db, "charge-1", null, default);
                await sink.MirrorRefundTotalAsync(sale, db, 3000, "payrexx", "partially-refunded", default);
                await sink.MirrorRefundTotalAsync(sale, db, 1000, "payrexx", "partially-refunded", default);
            }

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.Include(s => s.Refunds).SingleAsync();
            Assert.AreEqual(1, stored.Refunds.Count);
            Assert.AreEqual(3000, stored.Refunds.Sum(r => r.AmountMinor));
        }

        [TestMethod]
        public async Task FindSale_PrefersTheProviderSession()
        {
            var (sink, factory, _) = Build();
            await using var db = await factory.CreateDbContextAsync();
            await Seed(db, amountMinor: 5000, tenantId: 1, reference: "order-1", sessionId: "sess-a");
            await Seed(db, amountMinor: 7000, tenantId: 2, reference: "order-1", sessionId: "sess-b");

            var found = await sink.FindSaleAsync(db, "sess-b", "order-1", null, default);
            Assert.IsNotNull(found);
            Assert.AreEqual(2, found.TenantId);
        }

        [TestMethod]
        public async Task FindSale_RefusesToGuessBetweenTenants()
        {
            // Die Referenz ist nur JE MANDANT eindeutig. Ohne Mandant und mit mehreren Treffern lieber
            // nichts buchen als die Bestellung eines Fremden bezahlen.
            var (sink, factory, _) = Build();
            await using var db = await factory.CreateDbContextAsync();
            await Seed(db, amountMinor: 5000, tenantId: 1, reference: "order-1", sessionId: "sess-a");
            await Seed(db, amountMinor: 7000, tenantId: 2, reference: "order-1", sessionId: "sess-b");

            Assert.IsNull(await sink.FindSaleAsync(db, null, "order-1", null, default));
            // MIT Mandant ist sie eindeutig.
            var found = await sink.FindSaleAsync(db, null, "order-1", 2, default);
            Assert.IsNotNull(found);
            Assert.AreEqual(7000, found.AmountMinor);
        }

        private static (TenantSaleWebhookSink<PaymentsTestContext> Sink, IDbContextFactory<PaymentsTestContext> Factory,
            CountingObserver Observer) Build()
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var observer = new CountingObserver();
            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory,
                new TenantSaleNotifier([observer]));
            return (sink, factory, observer);
        }

        private static async Task<TenantSale> Seed(PaymentsTestContext db, long amountMinor, int tenantId = 1,
            string reference = "order-1", string sessionId = "sess-1")
        {
            var sale = new TenantSale
            {
                TenantId = tenantId,
                ExternalReference = reference,
                Description = "test",
                AmountMinor = amountMinor,
                Currency = "CHF",
                Status = TenantSaleStatus.Pending,
                ProviderSessionId = sessionId,
                CheckoutUrl = "https://example.invalid/pay",
                Provider = "test",
                Created = DateTime.UtcNow
            };
            db.TenantSales.Add(sale);
            await db.SaveChangesAsync();
            return sale;
        }

        private sealed class CountingObserver : ITenantSaleObserver
        {
            public int Completed { get; private set; }

            public int Refunded { get; private set; }

            public Task OnSaleCompletedAsync(TenantSale sale, CancellationToken cancellationToken = default)
            {
                Completed++;
                return Task.CompletedTask;
            }

            public Task OnSaleRefundedAsync(TenantSale sale, TenantSaleRefund refund,
                CancellationToken cancellationToken = default)
            {
                Refunded++;
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>Der kleinste Kontext, der <see cref="IPaymentsContext"/> erfüllt.</summary>
    public class PaymentsTestContext : DbContext, IPaymentsContext
    {
        public PaymentsTestContext(DbContextOptions<PaymentsTestContext> options) : base(options) { }

        public DbSet<TenantPaymentAccount> TenantPaymentAccounts { get; set; } = null!;

        public DbSet<TenantPaymentProfile> TenantPaymentProfiles { get; set; } = null!;

        public DbSet<TenantSale> TenantSales { get; set; } = null!;

        public DbSet<TenantSaleRefund> TenantSaleRefunds { get; set; } = null!;

        public DbSet<TenantFeeWaiver> TenantFeeWaivers { get; set; } = null!;
    }

    /// <summary>
    /// Die Fabrik dahinter.
    /// </summary>
    /// <remarks>
    /// Jeder Test bekommt seine eigene Ablage (der Name ist eine frische GUID), sonst sähe der zweite
    /// Test die Zeilen des ersten. Die Senke öffnet je Durchlauf einen eigenen Kontext — genau wie im
    /// Betrieb, wo ein Webhook nicht im Anfrage-Bereich eines Benutzers läuft.
    /// </remarks>
    public class PaymentsTestContextFactory : IDbContextFactory<PaymentsTestContext>
    {
        private readonly string databaseName;

        public PaymentsTestContextFactory(string databaseName)
        {
            this.databaseName = databaseName;
        }

        public PaymentsTestContext CreateDbContext()
            => new(new DbContextOptionsBuilder<PaymentsTestContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
