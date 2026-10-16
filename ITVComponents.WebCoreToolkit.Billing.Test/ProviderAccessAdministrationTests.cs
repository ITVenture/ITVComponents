using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Impl;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Options;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Impl;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Anlegen, Erneuern und Abschalten eigener Zugänge.
    /// </summary>
    /// <remarks>
    /// Was hier zählt: ein neues Konto überschreibt nie die Geheimnisse des alten (dessen Verkäufe werden darüber
    /// erstattet), es bleibt höchstens ein Zugang je Anbieter aktiv, und Geheimnisse landen nie in der Zeile.
    /// </remarks>
    [TestClass]
    public class ProviderAccessAdministrationTests
    {
        [TestMethod]
        public async Task Create_StoresSecretsUnderAFreshNameAndKeepsThemOutOfTheRow()
        {
            var (admin, factory, store, _) = Build();

            var id = await admin.CreateAsync(1, "test", "Mein Konto", new Dictionary<string, string?>
            {
                ["account"] = "acc-1", ["secret"] = "s3cr3t"
            });

            await using var db = await factory.CreateDbContextAsync();
            var row = await db.TenantProviderAccesses.SingleAsync();
            Assert.AreEqual(id, row.TenantProviderAccessId);
            Assert.AreEqual("acc-1", row.ProviderAccountId);
            StringAssert.StartsWith(row.ConnectionName, "payments-test-");
            Assert.IsFalse((row.SettingsJson ?? string.Empty).Contains("s3cr3t"), "A secret must never reach the row.");
            Assert.AreEqual("s3cr3t", store.Saved[row.ConnectionName]);
            Assert.IsNotNull(row.LastVerifiedUtc, "The new access is verified right away.");
        }

        [TestMethod]
        public async Task Create_ASecondAccountDisablesTheFirstAndLeavesItsSecretsAlone()
        {
            var (admin, factory, store, _) = Build();
            var first = await admin.CreateAsync(1, "test", "Alt", Values("acc-1", "old"));
            var second = await admin.CreateAsync(1, "test", "Neu", Values("acc-2", "new"));

            await using var db = await factory.CreateDbContextAsync();
            var rows = await db.TenantProviderAccesses.OrderBy(a => a.TenantProviderAccessId).ToListAsync();
            Assert.IsTrue(rows.Single(r => r.TenantProviderAccessId == first).Disabled);
            Assert.IsFalse(rows.Single(r => r.TenantProviderAccessId == second).Disabled);
            Assert.AreNotEqual(rows[0].ConnectionName, rows[1].ConnectionName);
            Assert.AreEqual("old", store.Saved[rows[0].ConnectionName], "The old account's sales are refunded with its own key.");
        }

        [TestMethod]
        public async Task Renew_ReplacesTheSecretOfTheSameAccess()
        {
            var (admin, factory, store, _) = Build();
            var id = await admin.CreateAsync(1, "test", "Konto", Values("acc-1", "old"));

            await admin.RenewSecretsAsync(1, id, new Dictionary<string, string?> { ["secret"] = "fresh" });

            await using var db = await factory.CreateDbContextAsync();
            var row = await db.TenantProviderAccesses.SingleAsync();
            Assert.AreEqual("fresh", store.Saved[row.ConnectionName]);
        }

        [TestMethod]
        public async Task Enable_IsRefusedWhileAnotherAccessIsActive()
        {
            var (admin, _, _, _) = Build();
            var first = await admin.CreateAsync(1, "test", "Alt", Values("acc-1", "a"));
            await admin.CreateAsync(1, "test", "Neu", Values("acc-2", "b"));

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => admin.SetDisabledAsync(1, first, false));

            Assert.AreEqual(PaymentErrorCodes.InvalidSettings, ex.Code);
        }

        [TestMethod]
        public async Task AForeignTenant_CannotTouchTheAccess()
        {
            var (admin, _, _, _) = Build();
            var id = await admin.CreateAsync(1, "test", "Konto", Values("acc-1", "a"));

            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => admin.SetDisabledAsync(2, id, true));
            await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => admin.VerifyAsync(2, id));
        }

        [TestMethod]
        public async Task AFailedVerification_IsRecordedOnTheRow()
        {
            var (admin, factory, _, provider) = Build();
            provider.Accept = false;

            await admin.CreateAsync(1, "test", "Konto", Values("acc-1", "a"));

            await using var db = await factory.CreateDbContextAsync();
            var row = await db.TenantProviderAccesses.SingleAsync();
            Assert.IsNull(row.LastVerifiedUtc);
            Assert.AreEqual("rejected", row.LastVerificationError);
        }

        [TestMethod]
        public void Payrexx_SplitsInstanceFromBothSecrets()
        {
            var settings = new StaticSettings<PayrexxOptions>(new PayrexxOptions());
            var provider = new PayrexxOwnAccessProvider(new PayrexxApiClient(new HttpClient(), settings));

            var draft = provider.Compose(new Dictionary<string, string?>
            {
                ["instance"] = "shop", ["apiSecret"] = "api", ["webhookSigningKey"] = "sign"
            });

            Assert.AreEqual("shop", draft.ProviderAccountId);
            Assert.IsNull(draft.SettingsJson);
            Assert.AreEqual("api", draft.Secrets.Single(s => s.ConnectionSuffix == string.Empty).Secret);
            Assert.AreEqual("sign", draft.Secrets.Single(s => s.ConnectionSuffix == "-webhook").Secret);
        }

        [TestMethod]
        public void Payrexx_WithoutSigningKey_IsRefused()
        {
            var settings = new StaticSettings<PayrexxOptions>(new PayrexxOptions());
            var provider = new PayrexxOwnAccessProvider(new PayrexxApiClient(new HttpClient(), settings));

            var ex = Assert.ThrowsExactly<TenantPaymentException>(() => provider.Compose(new Dictionary<string, string?>
            {
                ["instance"] = "shop", ["apiSecret"] = "api"
            }));

            Assert.AreEqual(PaymentErrorCodes.InvalidSettings, ex.Code);
        }

        [TestMethod]
        public void Wallee_PutsTheUserIdNextToTheKeyAsBasic()
        {
            var provider = new WalleeOwnAccessProvider(new StaticSettings<WalleeOptions>(new WalleeOptions()));

            var draft = provider.Compose(new Dictionary<string, string?>
            {
                ["spaceId"] = "4711", ["applicationUserId"] = "88", ["applicationUserKey"] = "k"
            });

            Assert.AreEqual("4711", draft.ProviderAccountId);
            var secret = draft.Secrets.Single();
            Assert.AreEqual(ExternalServiceAuthenticationType.Basic, secret.AuthenticationType);
            Assert.AreEqual("88", secret.ClientId);
            Assert.ThrowsExactly<TenantPaymentException>(() => provider.Compose(new Dictionary<string, string?>
            {
                ["spaceId"] = "abc", ["applicationUserId"] = "88", ["applicationUserKey"] = "k"
            }));
        }

        [TestMethod]
        public void SumUp_KeepsTheMerchantCodeAndTheKeyApart()
        {
            var api = new SumUpApiClient(new HttpClient(), new StaticSettings<SumUpOptions>(new SumUpOptions()));
            var provider = new SumUpOwnAccessProvider(api);

            var draft = provider.Compose(new Dictionary<string, string?> { ["merchantCode"] = "MC1", ["apiKey"] = "sup_sk_x" });

            Assert.AreEqual("MC1", draft.ProviderAccountId);
            Assert.AreEqual(ExternalServiceAuthenticationType.BearerToken, draft.Secrets.Single().AuthenticationType);
            Assert.IsNull(provider.WebhookPath, "SumUp is told the address per checkout; nothing to enter anywhere.");
        }

        private static Dictionary<string, string?> Values(string account, string secret)
            => new() { ["account"] = account, ["secret"] = secret };

        private static (ProviderAccessAdministration<PaymentsTestContext> Admin, IDbContextFactory<PaymentsTestContext> Factory,
            RecordingStore Store, TestProvider Provider) Build()
        {
            IDbContextFactory<PaymentsTestContext> factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var store = new RecordingStore();
            var provider = new TestProvider();
            return (new ProviderAccessAdministration<PaymentsTestContext>(factory, [provider], store), factory, store, provider);
        }

        private sealed class TestProvider : OwnAccessProviderBase
        {
            public bool Accept { get; set; } = true;

            public override string Key => "test";

            protected override IReadOnlyList<OwnAccessField> Fields { get; } =
            [
                new() { Name = "account", Required = true },
                new() { Name = "secret", Required = true, Secret = true }
            ];

            protected override OwnAccessDraft Build(IReadOnlyDictionary<string, string?> values)
                => new() { ProviderAccountId = Required(values, "account"), Secrets = BuildSecrets(values) };

            protected override IReadOnlyList<OwnAccessSecret> BuildSecrets(IReadOnlyDictionary<string, string?> values)
                => [new(string.Empty, ExternalServiceAuthenticationType.BearerToken, null, Required(values, "secret"))];

            public override Task<OwnAccessCheck> VerifyAsync(ProviderAccessSelection access, CancellationToken cancellationToken = default)
                => Task.FromResult(new OwnAccessCheck(Accept, Accept ? "ok" : "rejected"));
        }

        private sealed class RecordingStore : IProviderCredentialStore
        {
            public Dictionary<string, string> Saved { get; } = new();

            public Task<ProviderCredential> GetCredentialAsync(int tenantId, string connectionName, CancellationToken cancellationToken = default)
                => Task.FromResult(new ProviderCredential(ExternalServiceAuthenticationType.BearerToken, null, Saved[connectionName]));

            public Task SaveSecretAsync(int tenantId, string connectionName, ExternalServiceAuthenticationType authenticationType,
                string? clientId, string secret, CancellationToken cancellationToken = default)
            {
                Saved[connectionName] = secret;
                return Task.CompletedTask;
            }
        }
    }
}
