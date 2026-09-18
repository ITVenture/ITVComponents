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
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var observer = new CountingSaleObserver();
            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([observer]));
            var runtime = new PaymentsRuntime(new StaticSettings(new TenantPaymentsOptions
            {
                Enabled = true,
                DefaultCurrency = "CHF"
            }), new OpenGate());
            var device = new FakeDevice();
            return (new TestTerminalService(factory, runtime, sink, device), factory, device, observer);
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

            protected override Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
                => Task.FromResult(device.Start());

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

            public Task OnSaleCompletedAsync(TenantSale sale, CancellationToken cancellationToken = default)
            {
                Completed++;
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
