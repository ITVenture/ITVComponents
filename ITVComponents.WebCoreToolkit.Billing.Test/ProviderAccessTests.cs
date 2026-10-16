using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Der eigene Zugang eines Mandanten bei einem Anbieter (<see cref="TenantProviderAccess"/>).
    /// </summary>
    /// <remarks>
    /// Was hier festgenagelt wird, sind die Stellen, an denen ein Fehler Geld aufs falsche Konto lenkt: ein
    /// Verkauf, der still ueber die Plattform statt ueber den Zugang des Mandanten laeuft, und eine Erstattung,
    /// die nach einem Kontowechsel das neue Konto trifft statt das, auf dem bezahlt wurde.
    /// </remarks>
    [TestClass]
    public class ProviderAccessTests
    {
        [TestMethod]
        public async Task OwnAccess_IsRefusedByAConnectionThatCannotUseIt()
        {
            var factory = NewFactory();
            await SeedPlatformAccount(factory);
            await SeedAccess(factory, "test", "own-1");
            var service = new AccessSaleService(factory, Runtime(), supportsOwnAccess: false);

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => service.CreateSaleAsync(Request()));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
            Assert.AreEqual(0, service.Checkouts, "The sale must not be run over the platform access instead.");
            await using var db = await factory.CreateDbContextAsync();
            Assert.AreEqual(0, await db.TenantSales.CountAsync());
        }

        [TestMethod]
        public async Task OwnAccess_IsFrozenOnTheSaleAndNeedsNoPlatformAccount()
        {
            var factory = NewFactory();
            var accessId = await SeedAccess(factory, "test", "own-1");
            var service = new AccessSaleService(factory, Runtime(), supportsOwnAccess: true);

            var result = await service.CreateSaleAsync(Request());

            await using var db = await factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.AreEqual(accessId, sale.TenantProviderAccessId);
            Assert.AreEqual("own-1", sale.ProviderAccountId);
            Assert.IsNull(service.LastAccount, "Without a platform account the provider gets none.");
            Assert.AreEqual(accessId, service.LastAccess!.AccessId);
            Assert.AreEqual(TenantSaleStatus.Pending, result.Status);
        }

        [TestMethod]
        public async Task WithoutOwnAccess_ThePlatformAccountStillDecides()
        {
            var factory = NewFactory();
            await SeedPlatformAccount(factory);
            await SeedAccess(factory, "other", "own-elsewhere");
            var service = new AccessSaleService(factory, Runtime(), supportsOwnAccess: false);

            await service.CreateSaleAsync(Request());

            await using var db = await factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.IsNull(sale.TenantProviderAccessId, "An own access at ANOTHER provider must not touch this one.");
            Assert.AreEqual("acct_1", sale.ProviderAccountId);
        }

        [TestMethod]
        public async Task AfterAnAccountSwitch_AnOldSaleStaysOnTheOldAccess()
        {
            var factory = NewFactory();
            var oldId = await SeedAccess(factory, "test", "own-old");
            var service = new AccessSaleService(factory, Runtime(), supportsOwnAccess: true);
            await service.CreateSaleAsync(Request());

            // Der Mandant wechselt das Konto: alte Zeile ab, neue an.
            await using (var db = await factory.CreateDbContextAsync())
            {
                (await db.TenantProviderAccesses.SingleAsync()).Disabled = true;
                await db.SaveChangesAsync();
            }

            var newId = await SeedAccess(factory, "test", "own-new");
            var router = new ProviderAccessRouter<PaymentsTestContext>(factory);

            await using var check = await factory.CreateDbContextAsync();
            var oldSale = await check.TenantSales.SingleAsync();
            var forOld = await router.ForSaleAsync(oldSale, default);
            var forNew = await router.ForNewAsync(1, "test", default);

            Assert.AreEqual(oldId, forOld.AccessId);
            Assert.AreEqual("own-old", forOld.ProviderAccountId);
            Assert.IsTrue(forOld.Disabled);
            Assert.AreEqual(newId, forNew.AccessId);
        }

        [TestMethod]
        public async Task AFrozenAccessThatIsGone_IsNotReplacedByThePlatform()
        {
            var factory = NewFactory();
            var router = new ProviderAccessRouter<PaymentsTestContext>(factory);
            var sale = new TenantSale { TenantSaleId = 7, TenantId = 1, Provider = "test", TenantProviderAccessId = 99 };

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => router.ForSaleAsync(sale, default));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
        }

        [TestMethod]
        public async Task TwoActiveAccesses_AreRefusedInsteadOfPickingOne()
        {
            var factory = NewFactory();
            await SeedAccess(factory, "test", "own-1");
            await SeedAccess(factory, "test", "own-2");
            var router = new ProviderAccessRouter<PaymentsTestContext>(factory);

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => router.ForNewAsync(1, "test", default));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
        }

        [TestMethod]
        public async Task TerminalSale_WithOwnAccess_IsRefusedByAConnectionThatCannotUseIt()
        {
            var factory = NewFactory();
            await SeedPlatformAccount(factory);
            await SeedAccess(factory, "test", "own-1");
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.TenantPaymentTerminals.Add(new TenantPaymentTerminal
                {
                    TenantPaymentTerminalId = 5, TenantId = 1, Provider = "test", ProviderTerminalId = "dev-1",
                    DisplayName = "Kasse 1", Enabled = true, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
            var service = new AccessTerminalService(factory, Runtime(), sink);

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => service.StartPaymentAsync(new TerminalSaleRequest
            {
                TenantId = 1, TerminalId = 5, Amount = 12.50m, Currency = "CHF", Description = "Kaffee",
                ExternalReference = "bon-1"
            }));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
            Assert.AreEqual(0, service.Starts, "The device must not be driven over the platform access.");
        }

        [TestMethod]
        public async Task OnlineRouting_WithoutPlatformAccount_FollowsTheOwnAccess()
        {
            var factory = NewFactory();
            await SeedAccess(factory, "payrexx", "own-1");
            var router = new PaymentProviderRouter<PaymentsTestContext>(factory,
                [new NamedAdapter("stripe"), new NamedAdapter("payrexx")], "stripe");

            var chosen = await router.ForTenantAsync(1, default);

            Assert.AreEqual("payrexx", chosen.Key);
        }

        [TestMethod]
        public async Task OnlineRouting_ThePlatformAccountWinsOverAnOwnAccess()
        {
            var factory = NewFactory();
            await SeedPlatformAccount(factory, provider: "stripe");
            await SeedAccess(factory, "payrexx", "own-1");
            var router = new PaymentProviderRouter<PaymentsTestContext>(factory,
                [new NamedAdapter("stripe"), new NamedAdapter("payrexx")], null);

            var chosen = await router.ForTenantAsync(1, default);

            Assert.AreEqual("stripe", chosen.Key, "Where the tenant named a provider, that choice stands.");
        }

        private static IDbContextFactory<PaymentsTestContext> NewFactory() => new PaymentsTestContextFactory(Guid.NewGuid().ToString());

        private static PaymentsRuntime Runtime() => new(new StaticSettings<TenantPaymentsOptions>(new TenantPaymentsOptions
        {
            Enabled = true,
            DefaultCurrency = "CHF"
        }), new OpenGate());

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

        private static async Task SeedPlatformAccount(IDbContextFactory<PaymentsTestContext> factory, string? provider = null)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.TenantPaymentAccounts.Add(new TenantPaymentAccount
            {
                TenantId = 1,
                ProviderAccountId = "acct_1",
                Provider = provider,
                ChargesEnabled = true,
                PayoutsEnabled = true,
                DetailsSubmitted = true
            });
            await db.SaveChangesAsync();
        }

        private static async Task<int> SeedAccess(IDbContextFactory<PaymentsTestContext> factory, string provider, string accountId)
        {
            await using var db = await factory.CreateDbContextAsync();
            var access = new TenantProviderAccess
            {
                TenantId = 1,
                Provider = provider,
                ConnectionName = $"payments-{provider}-{accountId}",
                ProviderAccountId = accountId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };
            db.TenantProviderAccesses.Add(access);
            await db.SaveChangesAsync();
            return access.TenantProviderAccessId;
        }

        private sealed class AccessSaleService : TenantSaleServiceBase<PaymentsTestContext>
        {
            private readonly bool supportsOwnAccess;

            public AccessSaleService(IDbContextFactory<PaymentsTestContext> factory, PaymentsRuntime runtime, bool supportsOwnAccess)
                : base(factory, runtime, new TenantSaleNotifier([]))
            {
                this.supportsOwnAccess = supportsOwnAccess;
            }

            public int Checkouts { get; private set; }

            public TenantPaymentAccount? LastAccount { get; private set; }

            public ProviderAccessSelection? LastAccess { get; private set; }

            protected override string ProviderKey => "test";

            protected override bool SupportsOwnAccess => supportsOwnAccess;

            protected override async Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
                TenantPaymentAccount? account, int attempt, CancellationToken cancellationToken)
            {
                Checkouts++;
                LastAccount = account;
                LastAccess = await AccessForSaleAsync(sale, cancellationToken);
                return new ProviderCheckout($"page-{Checkouts}", $"https://pay.invalid/page-{Checkouts}");
            }

            protected override Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor, string? reason,
                bool refundApplicationFee, string accountId, long alreadyRefundedMinor, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            protected override Task<ProviderCancel> CancelCheckoutAsync(TenantSale sale, CancellationToken cancellationToken)
                => throw new NotSupportedException();
        }

        private sealed class AccessTerminalService : TerminalPaymentServiceBase<PaymentsTestContext>
        {
            public AccessTerminalService(IDbContextFactory<PaymentsTestContext> dbFactory, PaymentsRuntime runtime,
                TenantSaleWebhookSink<PaymentsTestContext> sink)
                : base(dbFactory, runtime, sink)
            {
            }

            public int Starts { get; private set; }

            protected override string ProviderKey => "test";

            protected override Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
            {
                Starts++;
                return Task.FromResult(new TerminalPaymentOutcome { State = TerminalPaymentState.InProgress });
            }

            protected override Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            protected override Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
                TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            protected override Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
                CancellationToken cancellationToken)
                => throw new NotSupportedException();
        }

        private sealed class NamedAdapter : IPaymentProviderAdapter
        {
            public NamedAdapter(string key)
            {
                Key = key;
            }

            public string Key { get; }

            public ITenantSaleService Sales => throw new NotSupportedException();

            public ITenantPaymentAccountService Accounts => throw new NotSupportedException();
        }

        private sealed class OpenGate : IPaymentFeatureGate
        {
            public Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
