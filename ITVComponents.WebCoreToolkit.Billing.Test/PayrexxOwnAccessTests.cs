using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Payrexx mit der eigenen Instanz eines Mandanten.
    /// </summary>
    /// <remarks>
    /// Festgenagelt wird, was bei einem Fehler Geld aufs falsche Konto lenkt oder fremde Verkaeufe bucht: der
    /// Aufruf geht an die Instanz und mit dem Schluessel des Mandanten, eine Meldung trifft nur Verkaeufe ihres
    /// Zugangs, und ohne Signierschluessel wird nichts gebucht.
    /// </remarks>
    [TestClass]
    public class PayrexxOwnAccessTests
    {
        private const string SigningKey = "own-signing-key";

        [TestMethod]
        public async Task Checkout_GoesToTheTenantsInstanceWithItsKey()
        {
            var env = await Env.CreateAsync();
            env.Http.Answer("""{"status":"success","data":[{"id":555,"link":"https://own.payrexx.com/pay/555"}]}""");

            var sale = await env.Sales.CreateSaleAsync(new SaleRequest
            {
                TenantId = 1, Amount = 50m, Currency = "CHF", Description = "Einkauf", ExternalReference = "order-1",
                SuccessUrl = "https://shop.invalid/ok", CancelUrl = "https://shop.invalid/abbruch"
            });

            var call = env.Http.Calls.Single();
            Assert.AreEqual("own-shop", QueryValue(call.Uri, "instance"));
            Assert.AreEqual("tenant-api-secret", call.ApiKey);
            StringAssert.StartsWith(call.Uri.ToString(), "https://api.payrexx.com/v1.0/Gateway/");
            Assert.AreEqual("https://own.payrexx.com/pay/555", sale.CheckoutUrl);

            await using var db = await env.Factory.CreateDbContextAsync();
            var row = await db.TenantSales.SingleAsync();
            Assert.AreEqual(env.AccessId, row.TenantProviderAccessId);
            Assert.AreEqual("own-shop", row.ProviderAccountId);
        }

        [TestMethod]
        public async Task Refund_UsesTheMerchantApiWithTheTransactionNumber()
        {
            var env = await Env.CreateAsync();
            await env.SeedOwnSaleAsync(paid: true, chargeId: "42");
            env.Http.Answer("""{"status":"success","data":[{"id":43,"status":"refunded"}]}""");

            await env.Sales.RefundSaleAsync(1, 2000, null);

            var call = env.Http.Calls.Single();
            StringAssert.Contains(call.Uri.AbsolutePath, "/Transaction/42/refund/");
            Assert.AreEqual("own-shop", QueryValue(call.Uri, "instance"));
            using var body = JsonDocument.Parse(call.Body!);
            Assert.AreEqual(2000, body.RootElement.GetProperty("amount").GetInt64(), "Rappen, as everywhere at Payrexx.");
        }

        [TestMethod]
        public async Task Webhook_OwnAccess_BooksWithTheTransactionNumber()
        {
            var env = await Env.CreateAsync();
            await env.SeedOwnSaleAsync(paid: false, chargeId: null);
            var body = Notice("confirmed");

            await env.Webhook.HandleOwnAsync(env.AccessId, body, Sign(body, SigningKey), "application/json", default);

            await using var db = await env.Factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.AreEqual(TenantSaleStatus.Paid, sale.Status);
            Assert.AreEqual("42", sale.ProviderChargeId, "The merchant API refunds by number, not by UUID.");
        }

        [TestMethod]
        public async Task Webhook_OwnAccess_WithoutSigningKey_IsRejected()
        {
            var env = await Env.CreateAsync(withSigningKey: false);
            await env.SeedOwnSaleAsync(paid: false, chargeId: null);
            var body = Notice("confirmed");

            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => env.Webhook.HandleOwnAsync(env.AccessId, body, Sign(body, SigningKey), "application/json", default));

            await using var db = await env.Factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Pending, (await db.TenantSales.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task Webhook_OnThePlatformEndpoint_DoesNotTouchAnOwnAccessSale()
        {
            // Gleiche Gateway-Nummer, andere Instanz: die Meldung der Plattform darf den Verkauf des Mandanten
            // nicht bezahlen.
            var env = await Env.CreateAsync();
            await env.SeedOwnSaleAsync(paid: false, chargeId: null);
            var body = Notice("confirmed");

            await env.Webhook.HandleAsync(body, Sign(body, "platform-key"), "application/json", default);

            await using var db = await env.Factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Pending, (await db.TenantSales.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task SaleSearch_IsLimitedToTheProvider()
        {
            // Der bestehende Fehler, den die Eingrenzung behebt: wallee-Transaktion 777 traf die Payrexx-Seite 777.
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            await using (var db = await ((IDbContextFactory<PaymentsTestContext>)factory).CreateDbContextAsync())
            {
                db.TenantSales.Add(new TenantSale
                {
                    TenantId = 1, ExternalReference = "order-1", Description = "x", AmountMinor = 100, Currency = "CHF",
                    Provider = "payrexx", ProviderSessionId = "777", Created = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
            await using var check = await ((IDbContextFactory<PaymentsTestContext>)factory).CreateDbContextAsync();

            Assert.IsNull(await sink.FindSaleAsync(check, SaleSearchScope.Platform("wallee"), "777", null, null, default));
            Assert.IsNotNull(await sink.FindSaleAsync(check, SaleSearchScope.Platform("payrexx"), "777", null, null, default));
        }

        private static string Notice(string status)
            => "{\"transaction\":{\"id\":42,\"uuid\":\"uuid-42\",\"status\":\"" + status + "\",\"referenceId\":\"order-1\",\"amount\":5000,\"currency\":\"CHF\",\"invoice\":{\"paymentRequestId\":777,\"referenceId\":\"order-1\"}}}";

        private static string Sign(string body, string secret)
            => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

        private static string? QueryValue(Uri uri, string name)
            => uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
                .Where(p => p[0] == name).Select(p => Uri.UnescapeDataString(p[1])).FirstOrDefault();

        private sealed class Env
        {
            public required IDbContextFactory<PaymentsTestContext> Factory { get; init; }

            public required FakePayrexx Http { get; init; }

            public required PayrexxSaleService<PaymentsTestContext> Sales { get; init; }

            public required PayrexxWebhookHandler<PaymentsTestContext> Webhook { get; init; }

            public int AccessId { get; init; }

            public static async Task<Env> CreateAsync(bool withSigningKey = true)
            {
                IDbContextFactory<PaymentsTestContext> factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
                int accessId;
                await using (var db = await factory.CreateDbContextAsync())
                {
                    var access = new TenantProviderAccess
                    {
                        TenantId = 1, Provider = "payrexx", ConnectionName = "payments-payrexx-1", ProviderAccountId = "own-shop",
                        Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                    };
                    db.TenantProviderAccesses.Add(access);
                    await db.SaveChangesAsync();
                    accessId = access.TenantProviderAccessId;
                }

                var http = new FakePayrexx();
                var settings = new StaticSettings<PayrexxOptions>(new PayrexxOptions
                {
                    Instance = "platform", ApiSecret = "platform-secret", WebhookSecret = "platform-key"
                });
                var credentials = new Credentials(withSigningKey);
                var sales = new PayrexxSaleService<PaymentsTestContext>(factory,
                    new PayrexxApiClient(new HttpClient(http), settings), new PayrexxServiceApiClient(new HttpClient(http), settings),
                    new StaticSettings<TenantPaymentsOptions>(new TenantPaymentsOptions { Enabled = true, DefaultCurrency = "CHF" }),
                    [new OpenGate()], [], credentials);
                var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
                return new Env
                {
                    Factory = factory,
                    Http = http,
                    Sales = sales,
                    Webhook = new PayrexxWebhookHandler<PaymentsTestContext>(sink, settings, credentials),
                    AccessId = accessId
                };
            }

            public async Task SeedOwnSaleAsync(bool paid, string? chargeId)
            {
                await using var db = await Factory.CreateDbContextAsync();
                db.TenantSales.Add(new TenantSale
                {
                    TenantId = 1, ExternalReference = "order-1", Description = "x", AmountMinor = 5000, Currency = "CHF",
                    Status = paid ? TenantSaleStatus.Paid : TenantSaleStatus.Pending, Provider = "payrexx",
                    ProviderSessionId = "777", ProviderChargeId = chargeId, ProviderAccountId = "own-shop",
                    TenantProviderAccessId = AccessId, CheckoutUrl = paid ? null : "https://own.payrexx.com/pay/777",
                    Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        private sealed class FakePayrexx : HttpMessageHandler
        {
            private string answer = """{"status":"success","data":[]}""";

            public List<(Uri Uri, string? ApiKey, string? Body)> Calls { get; } = new();

            public void Answer(string json) => answer = json;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Calls.Add((request.RequestUri!, request.Headers.TryGetValues("X-API-KEY", out var key) ? key.First() : null, body));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(answer, Encoding.UTF8, "application/json")
                };
            }
        }

        private sealed class Credentials : IProviderCredentialStore
        {
            private readonly bool withSigningKey;

            public Credentials(bool withSigningKey)
            {
                this.withSigningKey = withSigningKey;
            }

            public Task<ProviderCredential> GetCredentialAsync(int tenantId, string connectionName, CancellationToken cancellationToken = default)
            {
                if (connectionName == "payments-payrexx-1")
                {
                    return Task.FromResult(new ProviderCredential(ExternalServiceAuthenticationType.ApiKey, "X-API-KEY", "tenant-api-secret"));
                }

                if (connectionName == "payments-payrexx-1-webhook" && withSigningKey)
                {
                    return Task.FromResult(new ProviderCredential(ExternalServiceAuthenticationType.BearerToken, null, SigningKey));
                }

                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable, $"No credentials for '{connectionName}'.");
            }

            public Task SaveSecretAsync(int tenantId, string connectionName, ExternalServiceAuthenticationType authenticationType,
                string? clientId, string secret, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class OpenGate : IPaymentFeatureGate
        {
            public Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
