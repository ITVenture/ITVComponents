using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Test
{
    /// <summary>
    /// Weist nach, dass <see cref="OwnAccessHandler{TContext}"/> in JEDER Methode selbst prüft - und dass die
    /// Kopplung eines Terminals am Verwaltungsrecht hängt.
    /// </summary>
    /// <remarks>
    /// Derselbe Aufbau wie bei den Terminals: gesperrte Datenbank plus Attrappen, die ihre Aufrufe zählen. Bei den
    /// eigenen Zugängen geht es um Geheimnisse - ein Weg, der ohne Recht bis zur Verwaltung durchkommt, könnte einem
    /// Mandanten ein fremdes Konto unterschieben.
    /// </remarks>
    [TestClass]
    public class OwnAccessHandlerPermissionTests
    {
        private static OwnAccessHandler<BillingTestContext> Handler(HandlerTestEnvironment env, SpyAccessAdministration administration)
            => new(env.DbFactory(), env.Services, env.PaymentSettings(), env.FeatureGates(), administration);

        private static async Task AssertRefusedAsync(Func<Task> operation, string because)
        {
            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(operation, because);
            Assert.AreEqual(PaymentErrorCodes.NotPermitted, ex.Code);
        }

        [TestMethod]
        public async Task EveryPathRefusesWithoutPermission()
        {
            var env = HandlerTestEnvironment.WithPermissions("SomethingElse");
            env.DatabaseIsOffLimits = true;
            var administration = new SpyAccessAdministration();
            var handler = Handler(env, administration);

            Assert.ThrowsExactly<TenantPaymentException>(() => handler.GetProviders());
            Assert.ThrowsExactly<TenantPaymentException>(() => handler.DescribeFields("payrexx"));
            await AssertRefusedAsync(() => handler.GetAsync(), "auflisten");
            await AssertRefusedAsync(() => handler.CreateAsync("payrexx", "x", new Dictionary<string, string?>()), "verbinden");
            await AssertRefusedAsync(() => handler.RenewSecretsAsync(1, new Dictionary<string, string?>()), "erneuern");
            await AssertRefusedAsync(() => handler.SetDisabledAsync(1, true), "abschalten");
            await AssertRefusedAsync(() => handler.VerifyAsync(1), "pruefen");
            await AssertRefusedAsync(() => handler.SetUpWebhooksAsync(1, "https://app.invalid"), "Meldungen einrichten");

            Assert.AreEqual(0, administration.Calls.Count, "The administration was reached without permission.");
        }

        [TestMethod]
        public async Task ViewPermissionReadsButDoesNotManage()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ViewPermission);
            var administration = new SpyAccessAdministration();
            var handler = Handler(env, administration);

            handler.GetProviders();
            await handler.GetAsync();
            Assert.AreEqual(2, administration.Calls.Count);

            await AssertRefusedAsync(() => handler.CreateAsync("payrexx", "x", new Dictionary<string, string?>()), "verbinden");
            await AssertRefusedAsync(() => handler.VerifyAsync(1), "pruefen schreibt an die Zeile und redet mit dem Konto");
            Assert.AreEqual(2, administration.Calls.Count, "Nothing may get through after reading.");
        }

        [TestMethod]
        public async Task TheTenantComesFromTheScopeNeverFromTheCaller()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ManagePermission);
            env.CurrentTenantId = 7;
            var administration = new SpyAccessAdministration();
            var handler = Handler(env, administration);

            await handler.CreateAsync("payrexx", "Laden", new Dictionary<string, string?>());
            await handler.SetDisabledAsync(3, true);

            Assert.IsTrue(administration.Tenants.TrueForAll(t => t == 7));
        }

        [TestMethod]
        public async Task PairingATerminalNeedsManagement()
        {
            var env = HandlerTestEnvironment.WithPermissions(TerminalsHandler<BillingTestContext>.ViewPermission);
            var pairing = new SpyPairing();
            var handler = new TerminalsHandler<BillingTestContext>(env.DbFactory(), env.Services, new TerminalsHandlerPermissionTests.SpyTerminalAdministration(),
                new TerminalsHandlerPermissionTests.SpyTerminalCatalog(), new TerminalsHandlerPermissionTests.SpyTerminalPaymentService(), Array.Empty<ITerminalChoiceProvider>(),
                env.PaymentSettings(), env.FeatureGates(), [pairing]);

            Assert.IsTrue(handler.SupportsPairing("SUMUP"));
            Assert.IsFalse(handler.SupportsPairing("payrexx"));
            await AssertRefusedAsync(() => handler.PairAsync("sumup", "123456", "Solo"), "koppeln");
            Assert.AreEqual(0, pairing.Calls, "The device must not be paired without the right to manage terminals.");
        }

        private sealed class SpyPairing : ITerminalPairing
        {
            public int Calls { get; private set; }

            public string Key => "sumup";

            public Task<string> PairAsync(int tenantId, string pairingCode, string displayName, CancellationToken cancellationToken = default)
            {
                Calls++;
                return Task.FromResult("rdr_1");
            }
        }

        private sealed class SpyAccessAdministration : IProviderAccessAdministration
        {
            public List<string> Calls { get; } = new();

            public List<int> Tenants { get; } = new();

            public IReadOnlyList<string> GetProviders()
            {
                Calls.Add(nameof(GetProviders));
                return ["payrexx"];
            }

            public IReadOnlyList<OwnAccessField> DescribeFields(string provider)
            {
                Calls.Add(nameof(DescribeFields));
                return [];
            }

            public Task<IReadOnlyList<OwnAccessInfo>> GetAsync(int tenantId, CancellationToken cancellationToken = default)
                => Record<IReadOnlyList<OwnAccessInfo>>(tenantId, []);

            public Task<int> CreateAsync(int tenantId, string provider, string displayName, IReadOnlyDictionary<string, string?> values,
                CancellationToken cancellationToken = default)
                => Record(tenantId, 1);

            public Task RenewSecretsAsync(int tenantId, int accessId, IReadOnlyDictionary<string, string?> values,
                CancellationToken cancellationToken = default)
                => Record(tenantId, 0);

            public Task SetDisabledAsync(int tenantId, int accessId, bool disabled, CancellationToken cancellationToken = default)
                => Record(tenantId, 0);

            public Task<OwnAccessCheck> VerifyAsync(int tenantId, int accessId, CancellationToken cancellationToken = default)
                => Record(tenantId, new OwnAccessCheck(true, null));

            public Task<OwnAccessCheck> SetUpWebhooksAsync(int tenantId, int accessId, string publicBaseUrl,
                CancellationToken cancellationToken = default)
                => Record(tenantId, new OwnAccessCheck(true, null));

            private Task<T> Record<T>(int tenantId, T result, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            {
                Calls.Add(caller);
                Tenants.Add(tenantId);
                return Task.FromResult(result);
            }
        }
    }
}
