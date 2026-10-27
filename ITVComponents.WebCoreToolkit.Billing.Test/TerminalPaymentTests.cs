using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Das Kassieren am Terminal (Achse C).
    /// </summary>
    /// <remarks>
    /// Geprüft wird nicht, ob mit einem Gerät richtig geredet wird — das braucht ein echtes Gerät.
    /// Geprüft wird, was die Kasse vor einer doppelten Belastung schützt: dass ein zweiter Anlauf keinen
    /// zweiten Vorgang startet, dass eine ausgebliebene Antwort den Verkauf offen lässt statt ihn zu
    /// schliessen, und dass niemand auf einem fremden Gerät kassieren kann.
    /// </remarks>
    [TestClass]
    public class TerminalPaymentTests
    {
        [TestMethod]
        public async Task Start_ChargesOnceAndBooksTheSale()
        {
            var (service, factory, device, observer) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome
            {
                State = TerminalPaymentState.Succeeded,
                ProviderPaymentId = "pay-1",
                AmountMinor = 5000
            };

            var result = await service.StartPaymentAsync(Request());

            Assert.AreEqual(TerminalPaymentState.Succeeded, result.State);
            Assert.AreEqual(TenantSaleStatus.Paid, result.SaleStatus);
            Assert.IsFalse(result.KeepPolling);
            Assert.AreEqual(1, observer.Completed, "The shop must learn about the payment exactly once.");

            await using var db = await factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.AreEqual(1, sale.TenantPaymentTerminalId);
            Assert.AreEqual("pay-1", sale.ProviderChargeId);
            Assert.AreEqual("test", sale.Provider);
        }

        [TestMethod]
        public async Task Start_DoesNotStartASecondTimeForTheSameReference()
        {
            // Der Normalfall an einer Kasse, deren Netz hakt: derselbe Auftrag kommt zweimal an. Ein
            // zweiter Start waere eine zweite Belastung.
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome
            {
                State = TerminalPaymentState.InProgress,
                ProviderPaymentId = "pay-1"
            };

            await service.StartPaymentAsync(Request());
            var second = await service.StartPaymentAsync(Request());

            Assert.AreEqual(1, device.Starts, "A repeated request must not start a second payment.");
            Assert.AreEqual(1, device.Queries, "It must ASK about the running one instead.");
            Assert.IsTrue(second.KeepPolling);

            await using var db = await factory.CreateDbContextAsync();
            Assert.AreEqual(1, await db.TenantSales.CountAsync(), "And it must not create a second sale.");
        }

        [TestMethod]
        public async Task Start_LeavesTheSaleOpenWhenTheDeviceDoesNotAnswer()
        {
            // DER Fall, um den es geht. Eine Ausnahme heisst: wir wissen es nicht. Wer den Verkauf jetzt
            // als gescheitert schliesst, laesst die Kasse ein zweites Mal kassieren - auf eine Karte, die
            // vielleicht schon belastet ist.
            var (service, factory, device, observer) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Throw = new TerminalDeviceException("the line went down");

            var result = await service.StartPaymentAsync(Request());

            Assert.AreEqual(TerminalPaymentState.Unknown, result.State);
            Assert.IsTrue(result.KeepPolling, "An unclear outcome must keep the cash register asking.");
            Assert.AreEqual(0, observer.Completed);

            await using var db = await factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Pending, (await db.TenantSales.SingleAsync()).Status,
                "The sale must stay open — 'no answer' is not 'no payment'.");
        }

        [TestMethod]
        public async Task Start_KeepsTheProviderIdEvenWhenTheDeviceDoesNotAnswer()
        {
            // Stripe/wallee legen den Vorgang beim Anbieter an und stossen das Geraet erst danach an. Bleibt die
            // Antwort darauf aus, muss die Kennung trotzdem auf der Zeile stehen: sonst bleibt jedes Nachfragen
            // Unknown, und ein zweiter Start legte einen zweiten Vorgang an.
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.CreatedId = "pi_1";
            device.Throw = new TerminalDeviceException("the line went down");

            await service.StartPaymentAsync(Request());

            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.AreEqual("pi_1", (await db.TenantSales.SingleAsync()).ProviderSessionId,
                    "The id must be stored before the device is started.");
            }

            device.Throw = null;
            await service.StartPaymentAsync(Request());

            Assert.AreEqual(1, device.Starts, "With the id on record, a repeated start must not start again.");
            Assert.AreEqual(1, device.Queries, "It must ask about the running one instead.");
        }

        [TestMethod]
        public async Task Get_BooksWhatTheDeviceReportsLater()
        {
            // Der Weg zurueck nach einem Abbruch: die Kasse fragt nach, und jetzt steht das Ergebnis fest.
            var (service, factory, device, observer) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome { State = TerminalPaymentState.InProgress, ProviderPaymentId = "pay-1" };
            var started = await service.StartPaymentAsync(Request());

            device.Next = new TerminalPaymentOutcome
            {
                State = TerminalPaymentState.Succeeded,
                ProviderPaymentId = "pay-1",
                AmountMinor = 5000
            };
            var settled = await service.GetPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(TenantSaleStatus.Paid, settled.SaleStatus);
            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task Get_DoesNotAskAgainOnceTheSaleIsDecided()
        {
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome { State = TerminalPaymentState.Succeeded, ProviderPaymentId = "pay-1" };
            var started = await service.StartPaymentAsync(Request());

            var queriesBefore = device.Queries;
            var again = await service.GetPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(queriesBefore, device.Queries, "A settled sale must not bother the device.");
            Assert.AreEqual(TenantSaleStatus.Paid, again.SaleStatus);
        }

        [TestMethod]
        public async Task Start_RefusesATerminalOfAnotherTenant()
        {
            // Der Grund, warum die Kasse UNSEREN Schluessel schickt und nicht die Kennung beim Anbieter.
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 2);

            var request = Request();
            request.TenantId = 1;

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => service.StartPaymentAsync(request));
            Assert.AreEqual(0, device.Starts, "Nothing may reach a terminal that belongs to someone else.");
        }

        [TestMethod]
        public async Task Start_RefusesASwitchedOffTerminal()
        {
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1, enabled: false);

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => service.StartPaymentAsync(Request()));
            Assert.AreEqual(0, device.Starts);
        }

        [TestMethod]
        public async Task Cancel_LeavesTheSaleRunningWhenTheDeviceRefuses()
        {
            // Liegt die Karte schon auf, ist es zu spaet. "Laeuft weiter" ist dann die richtige Antwort -
            // eine Kasse, die einen Abbruch glaubt, der nicht stattfand, vergisst eine Zahlung.
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome { State = TerminalPaymentState.InProgress, ProviderPaymentId = "pay-1" };
            var started = await service.StartPaymentAsync(Request());

            device.Next = new TerminalPaymentOutcome { State = TerminalPaymentState.InProgress };
            var result = await service.CancelPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(TerminalPaymentState.InProgress, result.State);
            Assert.AreEqual(TenantSaleStatus.Pending, result.SaleStatus);
            Assert.IsTrue(result.KeepPolling);
        }

        [TestMethod]
        public async Task Start_IsRefusedWhenNoFeatureGateIsRegistered()
        {
            // Die Richtung, die im Zweifel gilt: kein Gate heisst NEIN. Lieber ein Verkauf, der nicht
            // zustande kommt, als eine Provision, auf die niemand Anspruch hatte.
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory,
                new TenantSaleNotifier([new CountingSaleObserver()]));
            var runtime = new PaymentsRuntime(new StaticSettings(new TenantPaymentsOptions
            {
                Enabled = true,
                DefaultCurrency = "CHF"
            }), null);
            var device = new FakeDevice();
            var service = new TestTerminalService(factory, runtime, sink, device);
            await Seed(factory, terminalId: 1, tenantId: 1);

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => service.StartPaymentAsync(Request()));
            Assert.AreEqual(0, device.Starts, "Nothing may reach a terminal while the feature is unproven.");
        }

        // ------------------------------------------------------------------ Belege (ISSUE TerminalReceipt)

        [TestMethod]
        public async Task Start_StoresReceiptAndTipBeforeTheObserverRuns()
        {
            // Der Beobachter bucht beim Host den Bon - er muss die Kartendaten schon am Verkauf vorfinden.
            var (service, factory, device, observer) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = Paid(new TerminalReceipt { Brand = "VISA", MaskedPan = "**** 4242", AuthorizationCode = "A1" }, tip: 300);

            var result = await service.StartPaymentAsync(Request());

            Assert.AreEqual(1, observer.Completed);
            Assert.IsNotNull(observer.LastReceiptJson, "The receipt must be on the sale BEFORE the observer is called.");
            Assert.AreEqual(300, observer.LastTipMinor);
            Assert.AreEqual("A1", result.Receipt?.AuthorizationCode);

            await using var db = await factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.AreEqual("VISA", TerminalReceiptStore.Of(sale)?.Brand);
            Assert.IsNull(sale.CompletionPendingSince);
        }

        [TestMethod]
        public async Task Get_ReturnsTheStoredReceiptWithoutAskingTheDevice()
        {
            // Nach einem "Spaeter klaeren" an der Kasse: das Geraet wird nicht mehr gefragt, die Angaben muessen
            // trotzdem kommen.
            var (service, factory, device, _) = Build();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = Paid(new TerminalReceipt { Brand = "VISA" }, tip: 150);
            var started = await service.StartPaymentAsync(Request());

            var queriesBefore = device.Queries;
            var again = await service.GetPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(queriesBefore, device.Queries);
            Assert.AreEqual("VISA", again.Receipt?.Brand);
            Assert.AreEqual(150, again.TipMinor);
        }

        [TestMethod]
        public async Task Webhook_FirstHoldsTheNotificationUntilTheTerminalDeliversTheReceipt()
        {
            var (service, factory, device, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome { State = TerminalPaymentState.InProgress, ProviderPaymentId = "pay-1" };
            var started = await service.StartPaymentAsync(Request());

            await PayByWebhookAsync(factory, sink, started.TenantSaleId);
            Assert.AreEqual(0, observer.Completed, "Paid, but without the receipt the observer must wait.");

            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await db.TenantSales.SingleAsync();
                Assert.AreEqual(TenantSaleStatus.Paid, sale.Status, "The money is in - the STATUS is not held back.");
                Assert.IsNotNull(sale.CompletionPendingSince);
            }

            device.Next = Paid(new TerminalReceipt { Brand = "MASTERCARD" });
            await service.GetPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(1, observer.Completed);
            Assert.IsNotNull(observer.LastReceiptJson, "The held-back notification must carry the receipt.");

            await service.GetPaymentAsync(started.TenantSaleId);
            Assert.AreEqual(1, observer.Completed, "Exactly once.");
        }

        [TestMethod]
        public async Task Pending_IsReleasedWithoutReceiptOnlyAfterTheWaitPeriod()
        {
            // Die Kasse ist aus - ohne Frist waere der Verkauf bezahlt, aber nie gebucht.
            var (service, factory, device, observer, sink) = BuildWithSink(receiptWaitSeconds: 3600);
            await Seed(factory, terminalId: 1, tenantId: 1);
            device.Next = new TerminalPaymentOutcome { State = TerminalPaymentState.InProgress, ProviderPaymentId = "pay-1" };
            var started = await service.StartPaymentAsync(Request());
            await PayByWebhookAsync(factory, sink, started.TenantSaleId);

            device.Throw = new TerminalDeviceException("till is off");
            await service.GetPaymentAsync(started.TenantSaleId);
            Assert.AreEqual(0, observer.Completed, "Within the wait period the receipt is still awaited.");

            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await db.TenantSales.SingleAsync();
                sale.CompletionPendingSince = DateTime.UtcNow.AddHours(-2);
                await db.SaveChangesAsync();
            }

            await service.GetPaymentAsync(started.TenantSaleId);
            Assert.AreEqual(1, observer.Completed, "After the wait period the sale is reported without the receipt.");
            Assert.IsNull(observer.LastReceiptJson);
        }

        [TestMethod]
        public async Task Release_HappensOnlyOnceAcrossConcurrentClaims()
        {
            // Terminal-Antwort, Webhook und Hintergrund-Durchlauf - auch aus verschiedenen Instanzen.
            var (_, factory, _, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.TenantSales.Add(new TenantSale
                {
                    TenantId = 1, ExternalReference = "order-1", Description = "test", AmountMinor = 5000,
                    Currency = "CHF", Status = TenantSaleStatus.Paid, TenantPaymentTerminalId = 1,
                    CompletionPendingSince = DateTime.UtcNow, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            await using var first = await factory.CreateDbContextAsync();
            await using var second = await factory.CreateDbContextAsync();
            var a = await first.TenantSales.SingleAsync();
            var b = await second.TenantSales.SingleAsync();

            var one = await sink.ReleasePendingCompletionAsync(a, first, default);
            var two = await sink.ReleasePendingCompletionAsync(b, second, default);

            Assert.IsTrue(one ^ two, "Exactly one claim may win.");
            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task TwoTerminalAnswersAtOnce_NotifyOnlyOnce()
        {
            // Die Kasse fragt nach, waehrend die SumUp-Rueckmeldung dasselbe tut - beide sehen noch Pending.
            var (_, factory, _, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory);

            await using var first = await factory.CreateDbContextAsync();
            await using var second = await factory.CreateDbContextAsync();
            var a = await first.TenantSales.SingleAsync();
            var b = await second.TenantSales.SingleAsync();

            var one = await sink.MarkTerminalPaidAsync(a, first, "pay-1", null, new TerminalReceipt { Brand = "VISA" }, null, default);
            var two = await sink.MarkTerminalPaidAsync(b, second, "pay-1", null, new TerminalReceipt { Brand = "VISA" }, null, default);

            Assert.IsTrue(one);
            Assert.IsFalse(two, "The second answer read the sale while it was unpaid and must not notify again.");
            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task TerminalFirst_ThenAStaleWebhook_DoesNotLeaveASecondNotificationBehind()
        {
            // Das Terminal bucht und meldet. Ein Webhook, der die Zeile noch unbezahlt gelesen hat, darf sie
            // danach nicht als "bezahlt, Meldung ausstehend" markieren - der Sweeper meldete sonst ein zweites Mal.
            var (_, factory, _, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory);

            await using var terminalDb = await factory.CreateDbContextAsync();
            await using var webhookDb = await factory.CreateDbContextAsync();
            var byTerminal = await terminalDb.TenantSales.SingleAsync();
            var byWebhook = await webhookDb.TenantSales.SingleAsync();

            Assert.IsTrue(await sink.MarkTerminalPaidAsync(byTerminal, terminalDb, "pay-1", null,
                new TerminalReceipt { Brand = "VISA" }, null, default));
            Assert.IsFalse(await sink.MarkPaidAsync(byWebhook, webhookDb, "pay-1", null, default));

            await using var check = await factory.CreateDbContextAsync();
            var stored = await check.TenantSales.SingleAsync();
            Assert.IsNull(stored.CompletionPendingSince, "Nothing may be left for the sweeper to report again.");
            Assert.IsNotNull(stored.TerminalReceiptJson);
            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task WebhookFirst_TheStaleTerminalAnswerReleasesWithItsReceipt()
        {
            // Der Webhook bucht ohne Beleg und haelt die Meldung zurueck. Die Antwort des Terminals hat die Zeile
            // noch unbezahlt gelesen: sie darf weder werfen noch verloren gehen, sondern gibt mit Beleg frei.
            var (_, factory, _, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory);

            await using var terminalDb = await factory.CreateDbContextAsync();
            await using var webhookDb = await factory.CreateDbContextAsync();
            var byTerminal = await terminalDb.TenantSales.SingleAsync();
            var byWebhook = await webhookDb.TenantSales.SingleAsync();

            Assert.IsTrue(await sink.MarkPaidAsync(byWebhook, webhookDb, "pay-1", null, default));
            Assert.AreEqual(0, observer.Completed, "Without a receipt the webhook holds the notification back.");

            var released = await sink.MarkTerminalPaidAsync(byTerminal, terminalDb, "pay-1", null,
                new TerminalReceipt { Brand = "VISA" }, null, default);

            Assert.IsTrue(released);
            Assert.AreEqual(1, observer.Completed);
            Assert.IsNotNull(observer.LastReceiptJson, "The observer must get the receipt the terminal delivered.");
            await using var check = await factory.CreateDbContextAsync();
            Assert.IsNull((await check.TenantSales.SingleAsync()).CompletionPendingSince);
        }

        [TestMethod]
        public async Task Reconcile_SettlesAnOpenSaleNobodyAsksAboutAnymore()
        {
            // Die Kasse ist weg, der Kunde hat am Geraet bezahlt. Ohne Rueckkanal bliebe der Verkauf Pending.
            var (service, factory, device, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 10);
            device.Next = Paid(new TerminalReceipt { Brand = "VISA" });

            await TerminalCompletionSweeper<PaymentsTestContext>.ReconcileOpenSalesAsync(
                Services(service, sink), factory, Options(), default);

            Assert.AreEqual(1, device.Queries);
            Assert.AreEqual(1, observer.Completed);
            await using var db = await factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Paid, (await db.TenantSales.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task Reconcile_LeavesASaleAloneWhileTheTillStillAsks()
        {
            var (service, factory, device, _, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 10, askedMinutesAgo: 0);

            await TerminalCompletionSweeper<PaymentsTestContext>.ReconcileOpenSalesAsync(
                Services(service, sink), factory, Options(), default);

            Assert.AreEqual(0, device.Queries, "A sale the till asked about a moment ago is the till's business.");
        }

        [TestMethod]
        public async Task Reconcile_DoesNotHammerAnUnreachableDevice()
        {
            // Agent getrennt: die Frage scheitert. Der Verkauf bleibt offen, wird aber erst nach dem Abstand
            // wieder gefragt - nicht in jedem Durchlauf.
            var (service, factory, device, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 10);
            device.Throw = new TerminalDeviceException("agent not connected");
            var services = Services(service, sink);

            await TerminalCompletionSweeper<PaymentsTestContext>.ReconcileOpenSalesAsync(services, factory, Options(), default);
            await TerminalCompletionSweeper<PaymentsTestContext>.ReconcileOpenSalesAsync(services, factory, Options(), default);

            Assert.AreEqual(1, device.Queries);
            Assert.AreEqual(0, observer.Completed);
            await using var db = await factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Pending, (await db.TenantSales.SingleAsync()).Status,
                "No answer is not 'not paid' - the sale stays open.");
        }

        [TestMethod]
        public async Task Reconcile_StillAsksAboutASaleOnASwitchedOffTerminal()
        {
            // Abgeschaltet nach dem Start: die Frage, ob die Karte belastet wurde, bleibt zu beantworten.
            var (service, factory, device, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1, enabled: false);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 10);
            device.Next = Paid(new TerminalReceipt { Brand = "VISA" });

            await TerminalCompletionSweeper<PaymentsTestContext>.ReconcileOpenSalesAsync(
                Services(service, sink), factory, Options(), default);

            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task SettleByHand_AsPaid_NotifiesOnceAndStopsTheReconciliation()
        {
            var (service, factory, device, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 60);

            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await db.TenantSales.SingleAsync();
                var status = await sink.SettleTerminalSaleByHandAsync(sale, db, true, "seen at the terminal", "tester",
                    "pay-77", default);
                Assert.AreEqual(TenantSaleStatus.Paid, status);
            }

            Assert.AreEqual(1, observer.Completed);
            await TerminalCompletionSweeper<PaymentsTestContext>.ReconcileOpenSalesAsync(
                Services(service, sink), factory, Options(), default);
            Assert.AreEqual(0, device.Queries, "A sale settled by hand is not asked about any more.");
        }

        [TestMethod]
        public async Task SettleByHand_AsNotPaid_CancelsAndALatePaymentIsStillBooked()
        {
            var (_, factory, _, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 60);

            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await db.TenantSales.SingleAsync();
                Assert.AreEqual(TenantSaleStatus.Canceled,
                    await sink.SettleTerminalSaleByHandAsync(sale, db, false, "card not charged", "tester", null, default));
            }

            // Das Geraet meldet doch noch eine Zahlung: das Geld ist da und darf nicht verschluckt werden.
            await using (var db = await factory.CreateDbContextAsync())
            {
                var sale = await db.TenantSales.SingleAsync();
                Assert.IsTrue(await sink.MarkTerminalPaidAsync(sale, db, "pay-1", null, null, null, default));
            }

            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task SettleByHand_LeavesASaleAloneThatWasDecidedMeanwhile()
        {
            var (_, factory, _, observer, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 60);

            await using var handDb = await factory.CreateDbContextAsync();
            await using var terminalDb = await factory.CreateDbContextAsync();
            var byHand = await handDb.TenantSales.SingleAsync();
            var byTerminal = await terminalDb.TenantSales.SingleAsync();

            await sink.MarkTerminalPaidAsync(byTerminal, terminalDb, "pay-1", null, null, null, default);
            var status = await sink.SettleTerminalSaleByHandAsync(byHand, handDb, false, "looked unpaid", "tester", null, default);

            Assert.AreEqual(TenantSaleStatus.Paid, status, "The terminal's answer wins over a stale manual decision.");
            Assert.AreEqual(1, observer.Completed);
        }

        [TestMethod]
        public async Task SettleByHand_NeedsAReason()
        {
            var (_, factory, _, _, sink) = BuildWithSink();
            await Seed(factory, terminalId: 1, tenantId: 1);
            await AddPendingTerminalSale(factory, startedMinutesAgo: 60);

            await using var db = await factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(
                () => sink.SettleTerminalSaleByHandAsync(sale, db, true, "  ", "tester", null, default));
            Assert.AreEqual(PaymentErrorCodes.ReasonRequired, ex.Code);
        }

        private static IServiceProvider Services(TestTerminalService service,
            TenantSaleWebhookSink<PaymentsTestContext> sink)
            => new ServiceCollection()
                .AddSingleton<ITerminalPaymentService>(service)
                .AddSingleton(sink)
                .BuildServiceProvider();

        private static TenantPaymentsOptions Options() => new()
        {
            Enabled = true,
            TerminalReconcileAfterSeconds = 120,
            TerminalReconcileIntervalSeconds = 60,
            TerminalReconcileMaxAgeHours = 24
        };

        private static async Task AddPendingTerminalSale(IDbContextFactory<PaymentsTestContext> factory,
            int startedMinutesAgo, int? askedMinutesAgo = null)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.TenantSales.Add(new TenantSale
            {
                TenantId = 1, ExternalReference = "order-1", Description = "test", AmountMinor = 5000,
                Currency = "CHF", Status = TenantSaleStatus.Pending, TenantPaymentTerminalId = 1,
                ProviderSessionId = "pay-1",
                Created = DateTime.UtcNow.AddMinutes(-startedMinutesAgo),
                Updated = DateTime.UtcNow.AddMinutes(-(askedMinutesAgo ?? startedMinutesAgo))
            });
            await db.SaveChangesAsync();
        }

        private static async Task AddPendingTerminalSale(IDbContextFactory<PaymentsTestContext> factory)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.TenantSales.Add(new TenantSale
            {
                TenantId = 1, ExternalReference = "order-1", Description = "test", AmountMinor = 5000,
                Currency = "CHF", Status = TenantSaleStatus.Pending, TenantPaymentTerminalId = 1,
                ProviderSessionId = "pay-1", Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        [TestMethod]
        public void Store_DropsACardNumberThatIsNotMasked()
        {
            var sale = new TenantSale { TenantSaleId = 9, TenantId = 1 };

            TerminalReceiptStore.Store(sale, new TerminalReceipt { Brand = "VISA", MaskedPan = "4242 4242 4242 4242" }, null);

            var stored = TerminalReceiptStore.Of(sale);
            Assert.AreEqual("VISA", stored?.Brand, "The rest of the receipt is kept.");
            Assert.IsNull(stored?.MaskedPan, "A full card number must never be stored.");
            StringAssert.DoesNotMatch(sale.TerminalReceiptJson!, new System.Text.RegularExpressions.Regex("4242 4242 4242"));

            TerminalReceiptStore.Store(sale, new TerminalReceipt { MaskedPan = "424242******4242" }, null);
            Assert.AreEqual("424242******4242", TerminalReceiptStore.Of(sale)?.MaskedPan, "First six and last four may stay.");
        }

        [TestMethod]
        public void TipOnReceipt_IsReadFromTheTerminalSettings()
        {
            Assert.IsTrue(TerminalReceipts.TipOnReceipt("{\"tipOnReceipt\":true}"));
            Assert.IsFalse(TerminalReceipts.TipOnReceipt("{\"tipOnReceipt\":false}"));
            Assert.IsFalse(TerminalReceipts.TipOnReceipt("{}"));
            Assert.IsFalse(TerminalReceipts.TipOnReceipt(null));
        }

        private static TerminalPaymentOutcome Paid(TerminalReceipt? receipt, long? tip = null) => new()
        {
            State = TerminalPaymentState.Succeeded,
            ProviderPaymentId = "pay-1",
            AmountMinor = 5000,
            TipMinor = tip,
            Receipt = receipt
        };

        private static async Task PayByWebhookAsync(IDbContextFactory<PaymentsTestContext> factory,
            TenantSaleWebhookSink<PaymentsTestContext> sink, int saleId)
        {
            await using var db = await factory.CreateDbContextAsync();
            var sale = await db.TenantSales.Include(s => s.Refunds).SingleAsync(s => s.TenantSaleId == saleId);
            await sink.MarkPaidAsync(sale, db, "pay-1", null, default);
        }

        private static TerminalSaleRequest Request() => new()
        {
            TenantId = 1,
            TerminalId = 1,
            ExternalReference = "order-1",
            Description = "test",
            Amount = 50.00m,
            Currency = "CHF"
        };

        private static (TestTerminalService Service, IDbContextFactory<PaymentsTestContext> Factory,
            FakeDevice Device, CountingSaleObserver Observer) Build()
        {
            var (service, factory, device, observer, _) = BuildWithSink();
            return (service, factory, device, observer);
        }

        private static (TestTerminalService Service, IDbContextFactory<PaymentsTestContext> Factory,
            FakeDevice Device, CountingSaleObserver Observer, TenantSaleWebhookSink<PaymentsTestContext> Sink)
            BuildWithSink(int receiptWaitSeconds = 60)
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var observer = new CountingSaleObserver();
            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([observer]));
            var runtime = new PaymentsRuntime(new StaticSettings(new TenantPaymentsOptions
            {
                Enabled = true,
                DefaultCurrency = "CHF",
                ReceiptWaitSeconds = receiptWaitSeconds
            }), new OpenGate());
            var device = new FakeDevice();
            return (new TestTerminalService(factory, runtime, sink, device), factory, device, observer, sink);
        }

        private static async Task Seed(IDbContextFactory<PaymentsTestContext> factory, int terminalId, int tenantId,
            bool enabled = true)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.TenantPaymentAccounts.Add(new TenantPaymentAccount
            {
                TenantId = tenantId,
                ProviderAccountId = "acct_1",
                ChargesEnabled = true,
                PayoutsEnabled = true,
                DetailsSubmitted = true
            });
            db.TenantPaymentTerminals.Add(new TenantPaymentTerminal
            {
                TenantPaymentTerminalId = terminalId,
                TenantId = tenantId,
                Provider = "test",
                ProviderTerminalId = "dev-1",
                DisplayName = "Kasse 1",
                Enabled = enabled,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        /// <summary>Eine Ausprägung, die nur mitzählt und zurückgibt, was der Test vorgibt.</summary>
        private sealed class FakeDevice
        {
            public TerminalPaymentOutcome Next { get; set; } = new() { State = TerminalPaymentState.InProgress };

            public Exception? Throw { get; set; }

            /// <summary>Die Kennung, die der Anbieter VOR dem Anstoss des Geraets vergibt (Stripe, wallee), oder null.</summary>
            public string? CreatedId { get; set; }

            public int Starts { get; private set; }

            public int Queries { get; private set; }

            public TerminalPaymentOutcome Start()
            {
                Starts++;
                return Answer();
            }

            public TerminalPaymentOutcome Query()
            {
                Queries++;
                return Answer();
            }

            private TerminalPaymentOutcome Answer()
            {
                if (Throw != null)
                {
                    throw Throw;
                }

                return Next;
            }
        }

        /// <summary>Die Basis mit einem Gerät, das der Test steuert.</summary>
        private sealed class TestTerminalService : TerminalPaymentServiceBase<PaymentsTestContext>
        {
            private readonly FakeDevice device;

            public TestTerminalService(IDbContextFactory<PaymentsTestContext> dbFactory, PaymentsRuntime runtime,
                TenantSaleWebhookSink<PaymentsTestContext> sink, FakeDevice device)
                : base(dbFactory, runtime, sink)
            {
                this.device = device;
            }

            protected override string ProviderKey => "test";

            protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
            {
                if (device.CreatedId != null)
                {
                    await RememberProviderPaymentIdAsync(sale, device.CreatedId, cancellationToken);
                }

                return device.Start();
            }

            protected override Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
                => Task.FromResult(device.Query());

            protected override Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
                => Task.FromResult(device.Query());

            protected override Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
                CancellationToken cancellationToken)
                => Task.FromResult(new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = true });
        }

        /// <summary>Ein Feature-Gate, das ja sagt.</summary>
        /// <remarks>
        /// Muss sein: OHNE Gate lehnt die Laufzeit ab, und das ist die richtige Richtung — lieber keinen
        /// Verkauf als eine Provision, auf die niemand Anspruch hatte. Der Test dafür steht unten.
        /// </remarks>
        private sealed class OpenGate : IPaymentFeatureGate
        {
            public Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }

        private sealed class CountingSaleObserver : ITenantSaleObserver
        {
            public int Completed { get; private set; }

            /// <summary>Was der Beobachter beim Melden am Verkauf vorfand.</summary>
            public string? LastReceiptJson { get; private set; }

            public long? LastTipMinor { get; private set; }

            public Task OnSaleCompletedAsync(TenantSale sale, CancellationToken cancellationToken = default)
            {
                Completed++;
                LastReceiptJson = sale.TerminalReceiptJson;
                LastTipMinor = sale.TipMinor;
                return Task.CompletedTask;
            }

            public Task OnSaleRefundedAsync(TenantSale sale, TenantSaleRefund refund,
                CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        /// <summary>Einstellungen ohne Ablage — derselbe Wert, egal wonach gefragt wird.</summary>
        private sealed class StaticSettings : IGlobalSettings<TenantPaymentsOptions>
        {
            public StaticSettings(TenantPaymentsOptions value)
            {
                Value = value;
            }

            public TenantPaymentsOptions Value { get; }

            public TenantPaymentsOptions ValueOrDefault => Value;

            public TenantPaymentsOptions GetValue(string explicitSettingName) => Value;

            public TenantPaymentsOptions GetValueOrDefault(string explicitSettingName) => Value;
        }
    }
}
