using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Das Abbrechen einer offenen Zahlungsseite (ISSUE MiniStore TenantSale CancelPendingCheckout).
    /// </summary>
    /// <remarks>
    /// Der Wert des Abbruchs ist die Antwort: danach muss der Aufrufer SICHER wissen, ob auf der Seite noch
    /// bezahlt werden kann. Ein falsches "abgebrochen" heisst, dass der Kunde an der Kasse ein zweites Mal
    /// zahlt.
    /// </remarks>
    [TestClass]
    public class TenantSaleCancelTests
    {
        [TestMethod]
        public async Task Cancel_ClosesThePageAndMovesToCanceled()
        {
            var (service, factory, _) = Build();
            var sale = await service.CreateSaleAsync(Request());
            Assert.IsNotNull(sale.CheckoutUrl);

            var result = await service.CancelSaleAsync(sale.TenantSaleId);

            Assert.AreEqual(TenantSaleStatus.Canceled, result.Status);
            Assert.IsNull(result.CheckoutUrl, "A cancelled sale must not offer its payment page any more.");
            CollectionAssert.AreEqual(new[] { "page-1" }, service.Cancelled);

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.SingleAsync();
            Assert.AreEqual(TenantSaleStatus.Canceled, stored.Status);
            Assert.IsNull(stored.CheckoutUrl);
        }

        [TestMethod]
        public async Task Cancel_IsHarmlessASecondTime()
        {
            var (service, _, _) = Build();
            var sale = await service.CreateSaleAsync(Request());

            await service.CancelSaleAsync(sale.TenantSaleId);
            var second = await service.CancelSaleAsync(sale.TenantSaleId);

            Assert.AreEqual(TenantSaleStatus.Canceled, second.Status);
            Assert.AreEqual(1, service.Cancelled.Count, "A sale that is no longer pending must not reach the provider again.");
        }

        [TestMethod]
        public async Task Cancel_ReportsAndBooksAPaymentThatWasFaster()
        {
            // Der eigentliche Fall: der alte Tab hat bezahlt, bevor die Seite geschlossen wurde. Der Aufrufer
            // muss "bezahlt" hoeren - und die Beobachter genau einmal, auch wenn der Webhook danach kommt.
            var (service, factory, observer) = Build();
            var sale = await service.CreateSaleAsync(Request());
            service.NextCancel = new ProviderCancel(ProviderCancelOutcome.Paid, "charge-1", "buyer@example.com");

            var result = await service.CancelSaleAsync(sale.TenantSaleId);

            Assert.AreEqual(TenantSaleStatus.Paid, result.Status);
            Assert.IsNull(result.CheckoutUrl);
            Assert.AreEqual(1, observer.Completed);

            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([observer]));
            await using (var db = await factory.CreateDbContextAsync())
            {
                var row = await db.TenantSales.Include(s => s.Refunds).SingleAsync();
                Assert.IsFalse(await sink.MarkPaidAsync(row, db, "charge-1", null, default),
                    "The webhook that follows is a repeat, not a second release.");
                Assert.AreEqual("charge-1", row.ProviderChargeId);
            }

            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task Cancel_LeavesTheSalePendingWhileAPaymentIsUnderway()
        {
            var (service, factory, observer) = Build();
            var sale = await service.CreateSaleAsync(Request());
            service.NextCancel = new ProviderCancel(ProviderCancelOutcome.InProgress, Detail: "processing");

            var result = await service.CancelSaleAsync(sale.TenantSaleId);

            Assert.AreEqual(TenantSaleStatus.Pending, result.Status, "Pending is the honest answer: the page may still be paid.");
            Assert.IsNotNull(result.CheckoutUrl);
            Assert.AreEqual(0, observer.Completed);
            await using var check = await factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Pending, (await check.TenantSales.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task Cancel_WithoutAPageNeedsNoProvider()
        {
            // Die Seite ist nie entstanden - der Anbieter hat beim Anlegen abgelehnt.
            var (service, _, _) = Build();
            service.FailCheckout = true;
            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => service.CreateSaleAsync(Request()));
            service.FailCheckout = false;
            var pending = await service.FindByReferenceAsync(1, "order-1");
            Assert.IsNotNull(pending);

            var result = await service.CancelSaleAsync(pending.TenantSaleId);

            Assert.AreEqual(TenantSaleStatus.Canceled, result.Status);
            Assert.AreEqual(0, service.Cancelled.Count);
        }

        [TestMethod]
        public async Task CreateAfterCancel_IssuesAFreshPageEveryTime()
        {
            // Ein Abbruch darf die Referenz nicht sperren. Und der DRITTE Anlauf muss vom zweiten
            // unterscheidbar sein - sonst gibt ein Idempotenz-Schluessel dem Kunden die tote Seite zurueck.
            var (service, _, _) = Build();
            var first = await service.CreateSaleAsync(Request());
            await service.CancelSaleAsync(first.TenantSaleId);

            var second = await service.CreateSaleAsync(Request());
            Assert.AreEqual(TenantSaleStatus.Pending, second.Status);
            Assert.AreEqual(first.TenantSaleId, second.TenantSaleId, "It is a new attempt at the same sale, not a second sale.");
            Assert.AreEqual("https://pay.invalid/page-2", second.CheckoutUrl);

            await service.CancelSaleAsync(second.TenantSaleId);
            var third = await service.CreateSaleAsync(Request());
            Assert.AreEqual("https://pay.invalid/page-3", third.CheckoutUrl);

            CollectionAssert.AreEqual(new[] { "first", "page-1", "page-2" }, service.PreviousPages,
                "Each attempt must see the page it replaces, so a provider can key it apart.");
        }

        [TestMethod]
        public async Task LateExpiryOfTheReplacedPage_DoesNotTouchTheNewOne()
        {
            // Abbruch, sofort neu ausgestellt - und dann kommt das 'expired' der ALTEN Seite.
            var (service, factory, _) = Build();
            var first = await service.CreateSaleAsync(Request());
            await service.CancelSaleAsync(first.TenantSaleId);
            await service.CreateSaleAsync(Request());

            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
            await using (var db = await factory.CreateDbContextAsync())
            {
                var row = await db.TenantSales.Include(s => s.Refunds).SingleAsync();
                await sink.MoveToAsync(row, db, TenantSaleStatus.Expired, default, "page-1");
            }

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.SingleAsync();
            Assert.AreEqual(TenantSaleStatus.Pending, stored.Status);
            Assert.AreEqual("https://pay.invalid/page-2", stored.CheckoutUrl);
        }

        [TestMethod]
        public async Task PaymentOnACanceledSale_IsStillBooked()
        {
            // Sollte nach einem Abbruch doch noch Geld ankommen, ist es da. Es zu ignorieren, hiesse eine
            // Zahlung zu verschlucken, die dann niemand mehr findet.
            var (service, factory, observer) = Build();
            var sale = await service.CreateSaleAsync(Request());
            await service.CancelSaleAsync(sale.TenantSaleId);

            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([observer]));
            await using (var db = await factory.CreateDbContextAsync())
            {
                var row = await db.TenantSales.Include(s => s.Refunds).SingleAsync();
                Assert.IsTrue(await sink.MarkPaidAsync(row, db, "charge-late", null, default));
            }

            await using var check = await factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Paid, (await check.TenantSales.SingleAsync()).Status);
            Assert.AreEqual(1, observer.Completed);
        }

        private static SaleRequest Request() => new()
        {
            TenantId = 1,
            Amount = 49.90m,
            Currency = "CHF",
            Description = "Einkauf",
            ExternalReference = "order-1",
            SuccessUrl = "https://shop.invalid/ok",
            CancelUrl = "https://shop.invalid/abbruch"
        };

        private static (FakeSaleService Service, IDbContextFactory<PaymentsTestContext> Factory, CountingObserver Observer) Build()
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            using (var db = factory.CreateDbContext())
            {
                db.TenantPaymentAccounts.Add(new TenantPaymentAccount
                {
                    TenantId = 1,
                    ProviderAccountId = "acct_1",
                    ChargesEnabled = true,
                    PayoutsEnabled = true,
                    DetailsSubmitted = true
                });
                db.SaveChanges();
            }

            var observer = new CountingObserver();
            var runtime = new PaymentsRuntime(new StaticSettings<TenantPaymentsOptions>(new TenantPaymentsOptions
            {
                Enabled = true,
                DefaultCurrency = "CHF"
            }), new OpenGate());
            return (new FakeSaleService(factory, runtime, new TenantSaleNotifier([observer])), factory, observer);
        }

        /// <summary>Ein Anbieter, der nur mitschreibt und zurueckgibt, was der Test vorgibt.</summary>
        private sealed class FakeSaleService : TenantSaleServiceBase<PaymentsTestContext>
        {
            private int pages;

            public FakeSaleService(IDbContextFactory<PaymentsTestContext> factory, PaymentsRuntime runtime,
                TenantSaleNotifier notifier) : base(factory, runtime, notifier)
            {
            }

            public ProviderCancel NextCancel { get; set; } = new(ProviderCancelOutcome.Canceled);

            public bool FailCheckout { get; set; }

            public List<string> Cancelled { get; } = new();

            public List<string> PreviousPages { get; } = new();

            protected override string ProviderKey => "test";

            protected override Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
                TenantPaymentAccount? account, int attempt, CancellationToken cancellationToken)
            {
                if (FailCheckout)
                {
                    throw new TenantPaymentException(PaymentErrorCodes.ProviderError, "refused");
                }

                PreviousPages.Add(sale.ProviderSessionId ?? "first");
                pages++;
                return Task.FromResult(new ProviderCheckout($"page-{pages}", $"https://pay.invalid/page-{pages}"));
            }

            protected override Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor, string? reason,
                bool refundApplicationFee, string accountId, long alreadyRefundedMinor, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            protected override Task<ProviderCancel> CancelCheckoutAsync(TenantSale sale, CancellationToken cancellationToken)
            {
                Cancelled.Add(sale.ProviderSessionId!);
                return Task.FromResult(NextCancel);
            }
        }

        private sealed class OpenGate : IPaymentFeatureGate
        {
            public Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }

        private sealed class CountingObserver : ITenantSaleObserver
        {
            public int Completed { get; private set; }

            public Task OnSaleCompletedAsync(TenantSale sale, CancellationToken cancellationToken = default)
            {
                Completed++;
                return Task.CompletedTask;
            }

            public Task OnSaleRefundedAsync(TenantSale sale, TenantSaleRefund refund,
                CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
