using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Impl;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Options;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
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
    /// Kassieren an einem SumUp Solo - gegen einen nachgebauten Endpunkt, nicht gegen SumUp.
    /// </summary>
    /// <remarks>
    /// Was hier steht, ist die Lesart der Doku, nicht ein Nachweis gegen die Sandbox. Festgenagelt wird, was
    /// bei einem Fehler Geld kostet: Rappen statt Franken im Checkout, Franken statt Rappen bei der Erstattung,
    /// "besetzt" nicht als gescheitert, ein unbestaetigter Abbruch nicht als abgebrochen, und die Meldung an
    /// return_url nur als Anstoss.
    /// </remarks>
    [TestClass]
    public class SumUpTerminalTests
    {
        [TestMethod]
        public async Task Start_SendsMinorUnitsAffiliateAndTheTenantsKey()
        {
            var env = await Env.CreateAsync();
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/checkout", 201,
                """{"data":{"checkout_id":"chk-1","client_transaction_id":"ctx-1"}}""");

            var result = await env.Service.StartPaymentAsync(Env.Request(12.50m));

            Assert.AreEqual(TerminalPaymentState.InProgress, result.State);
            var call = env.Http.Calls.Single();
            Assert.AreEqual("Bearer sup_sk_test_tenant", call.Authorization);
            using var body = JsonDocument.Parse(call.Body!);
            var amount = body.RootElement.GetProperty("total_amount");
            Assert.AreEqual(1250, amount.GetProperty("value").GetInt64(), "Rappen, not francs.");
            Assert.AreEqual(2, amount.GetProperty("minor_unit").GetInt32());
            Assert.AreEqual("CHF", amount.GetProperty("currency").GetString());
            var affiliate = body.RootElement.GetProperty("affiliate");
            Assert.AreEqual("app-1", affiliate.GetProperty("app_id").GetString());
            Assert.AreEqual(result.TenantSaleId.ToString(), affiliate.GetProperty("foreign_transaction_id").GetString());
            Assert.AreEqual("https://shop.invalid/sumup", body.RootElement.GetProperty("return_url").GetString());
            CollectionAssert.AreEqual(new[] { 0.05m, 0.10m },
                body.RootElement.GetProperty("tip_rates").EnumerateArray().Select(e => e.GetDecimal()).ToArray());

            await using var db = await env.Factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.AreEqual("ctx-1", sale.ProviderSessionId);
            Assert.AreEqual("chk-1", sale.ProviderPaymentIntentId);
            Assert.AreEqual("MC123", sale.ProviderAccountId);
            Assert.IsNotNull(sale.TenantProviderAccessId);
        }

        [TestMethod]
        public async Task Start_WithoutOwnAccess_IsRefusedBeforeAnyCall()
        {
            var env = await Env.CreateAsync(withAccess: false);

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => env.Service.StartPaymentAsync(Env.Request(10m)));

            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
            Assert.AreEqual(0, env.Http.Calls.Count);
        }

        [TestMethod]
        public async Task Start_ReaderBusy_StaysOpenInsteadOfFailing()
        {
            var env = await Env.CreateAsync();
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/checkout", 422,
                """{"errors":{"detail":"There is a pending checkout for the device.","type":"READER_BUSY"}}""");

            var result = await env.Service.StartPaymentAsync(Env.Request(10m));

            Assert.AreEqual(TerminalPaymentState.Unknown, result.State, "Busy may be OUR earlier attempt - never call it failed.");
            Assert.AreEqual(TenantSaleStatus.Pending, result.SaleStatus);
        }

        [TestMethod]
        public async Task Start_ReaderOffline_FailsSafely()
        {
            var env = await Env.CreateAsync();
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/checkout", 422,
                """{"type":"https://developer.sumup.com/problem/reader-offline","detail":"The device is offline."}""");

            var result = await env.Service.StartPaymentAsync(Env.Request(10m));

            Assert.AreEqual(TerminalPaymentState.Failed, result.State);
            Assert.AreEqual("READER_OFFLINE", result.FailureCode);
        }

        [TestMethod]
        public async Task Query_Successful_BooksTheTransactionIdAndAMaskedReceipt()
        {
            var env = await Env.CreateAsync();
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/checkout", 201,
                """{"data":{"checkout_id":"chk-1","client_transaction_id":"ctx-1"}}""");
            var started = await env.Service.StartPaymentAsync(Env.Request(12.50m));

            env.Http.On("GET", "/v0.1/merchants/MC123/readers/rdr_1/checkout/chk-1", 200,
                """{"data":{"checkout_id":"chk-1","client_transaction_id":"ctx-1","status":"successful"}}""");
            env.Http.On("GET", "/v2.1/merchants/MC123/transactions", 200,
                """{"id":"tx-9","transaction_code":"TC9","amount":12.50,"currency":"CHF","status":"SUCCESSFUL","timestamp":"2026-10-16T10:00:00Z","card":{"last_4_digits":"4242","type":"VISA"}}""");
            env.Http.On("GET", "/v1.1/receipts/tx-9", 200,
                """{"transaction_data":{"verification_method":"PIN","card":{"last_4_digits":"4242","type":"VISA"}},"acquirer_data":{"tid":"T-77","authorization_code":"A1B2"}}""");

            var result = await env.Service.GetPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(TerminalPaymentState.Succeeded, result.State);
            Assert.AreEqual(TenantSaleStatus.Paid, result.SaleStatus);
            StringAssert.Contains(env.Http.Calls.Single(c => c.Path.StartsWith("/v2.1")).Query, $"foreign_transaction_id={started.TenantSaleId}",
                "The transaction is found by OUR id - the one that is known even when the start got no answer.");

            await using var db = await env.Factory.CreateDbContextAsync();
            var sale = await db.TenantSales.SingleAsync();
            Assert.AreEqual("tx-9", sale.ProviderChargeId, "Refunds address the transaction id.");
            var receipt = TerminalReceiptStore.Of(sale)!;
            Assert.AreEqual("**** **** **** 4242", receipt.MaskedPan);
            Assert.AreEqual("A1B2", receipt.AuthorizationCode);
            Assert.AreEqual("T-77", receipt.TerminalId);
            Assert.AreEqual("MC123", receipt.MerchantId);
        }

        [TestMethod]
        public async Task Cancel_IsNotTakenForCancelledUntilSumUpSaysSo()
        {
            var env = await Env.CreateAsync();
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/checkout", 201,
                """{"data":{"checkout_id":"chk-1","client_transaction_id":"ctx-1"}}""");
            var started = await env.Service.StartPaymentAsync(Env.Request(10m));
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/terminate", 202, "{}");

            var result = await env.Service.CancelPaymentAsync(started.TenantSaleId);

            Assert.AreEqual(TerminalPaymentState.InProgress, result.State);
            Assert.AreEqual(TenantSaleStatus.Pending, result.SaleStatus);
        }

        [TestMethod]
        public async Task Webhook_IsOnlyATriggerAndChecksTheMerchant()
        {
            var env = await Env.CreateAsync();
            env.Http.On("POST", "/v0.1/merchants/MC123/readers/rdr_1/checkout", 201,
                """{"data":{"checkout_id":"chk-1","client_transaction_id":"ctx-1"}}""");
            await env.Service.StartPaymentAsync(Env.Request(10m));
            env.Http.On("GET", "/v0.1/merchants/MC123/readers/rdr_1/checkout/chk-1", 200,
                """{"data":{"status":"failed","payment_failure_reason":"declined"}}""");
            var handler = new SumUpWebhookHandler<PaymentsTestContext>(env.Factory, env.Service);

            Assert.AreEqual(SumUpWebhookOutcome.UnknownSale, await handler.HandleAsync(Notice("ctx-other", "MC123", "successful"), default));
            Assert.AreEqual(SumUpWebhookOutcome.Rejected, await handler.HandleAsync(Notice("ctx-1", "MC-FOREIGN", "successful"), default));
            // Die Meldung sagt "successful", SumUp selbst "failed" - es gilt, was SumUp auf Nachfrage sagt.
            Assert.AreEqual(SumUpWebhookOutcome.Processed, await handler.HandleAsync(Notice("ctx-1", "MC123", "successful"), default));

            await using var db = await env.Factory.CreateDbContextAsync();
            Assert.AreEqual(TenantSaleStatus.Failed, (await db.TenantSales.SingleAsync()).Status);
        }

        [TestMethod]
        public async Task Refund_SendsMajorUnitsAgainstTheTransaction()
        {
            var env = await Env.CreateAsync();
            await using (var db = await env.Factory.CreateDbContextAsync())
            {
                db.TenantSales.Add(new TenantSale
                {
                    TenantId = 1, ExternalReference = "bon-1", Description = "x", AmountMinor = 1250, Currency = "CHF",
                    Status = TenantSaleStatus.Paid, Provider = "sumup", ProviderChargeId = "tx-9",
                    ProviderAccountId = "MC123", TenantProviderAccessId = env.AccessId, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            env.Http.On("POST", "/v1.0/merchants/MC123/payments/tx-9/refunds", 200, "{}");
            var sales = new SumUpSaleService<PaymentsTestContext>(env.Factory, env.Api, env.Credentials, Env.PaymentSettings(), [new OpenGate()], []);

            var refund = await sales.RefundSaleAsync(1, 500, "zu viel");

            Assert.AreEqual(TenantSaleStatus.PartiallyRefunded, refund.SaleStatus);
            using var body = JsonDocument.Parse(env.Http.Calls.Single().Body!);
            Assert.AreEqual(5.00m, body.RootElement.GetProperty("amount").GetDecimal(), "Francs here - not Rappen.");
        }

        [TestMethod]
        public void TipRates_ArePercentagesTurnedIntoSortedShares()
        {
            var terminal = new TenantPaymentTerminal { ProviderTerminalId = "rdr_1", ConfigurationJson = """{"tipRates":"15, 5;10, 120, x"}""" };

            CollectionAssert.AreEqual(new[] { 0.05m, 0.10m, 0.15m }, SumUpTerminalPaymentService<PaymentsTestContext>.TipRatesOf(terminal));
            Assert.IsNull(SumUpTerminalPaymentService<PaymentsTestContext>.TipRatesOf(new TenantPaymentTerminal { ConfigurationJson = "{}" }));
        }

        [TestMethod]
        public void ErrorType_IsReadFromAllThreeShapes()
        {
            Assert.AreEqual("READER_BUSY", SumUpApiClient.ErrorTypeOf("""{"errors":{"type":"READER_BUSY"}}"""));
            Assert.AreEqual("READER_OFFLINE", SumUpApiClient.ErrorTypeOf("""{"type":"https://developer.sumup.com/problem/reader-offline"}"""));
            Assert.AreEqual("NOT_FOUND", SumUpApiClient.ErrorTypeOf("""{"error_code":"NOT_FOUND","message":"x"}"""));
            Assert.IsNull(SumUpApiClient.ErrorTypeOf("<html>"));
        }

        [TestMethod]
        public async Task OnlineRouting_DoesNotCountATerminalOnlyProvider()
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var router = new PaymentProviderRouter<PaymentsTestContext>(factory,
            [
                new PaymentProviderAdapter("stripe", new NoSales(), new NoAccounts()),
                new PaymentProviderAdapter("sumup", new NoSales(), new NoAccounts(), supportsOnlineSales: false)
            ], null);

            Assert.AreEqual("stripe", (await router.ForTenantAsync(1, default)).Key,
                "Adding SumUp terminals must not turn a host with ONE online provider into one with two.");
        }

        private static string Notice(string clientTransactionId, string merchant, string status)
            => $$"""{"id":"evt-1","event_type":"solo.transaction.updated","payload":{"client_transaction_id":"{{clientTransactionId}}","merchant_code":"{{merchant}}","status":"{{status}}"},"timestamp":"2026-10-16T10:00:00Z"}""";

        /// <summary>Alles, was ein Test braucht: Ablage, Geraet, Zugang, nachgebauter Endpunkt.</summary>
        private sealed class Env
        {
            public required IDbContextFactory<PaymentsTestContext> Factory { get; init; }

            public required FakeSumUp Http { get; init; }

            public required SumUpApiClient Api { get; init; }

            public required FixedCredentials Credentials { get; init; }

            public required SumUpTerminalPaymentService<PaymentsTestContext> Service { get; init; }

            public int AccessId { get; init; }

            public static async Task<Env> CreateAsync(bool withAccess = true)
            {
                var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
                var accessId = 0;
                await using (var db = await ((IDbContextFactory<PaymentsTestContext>)factory).CreateDbContextAsync())
                {
                    db.TenantPaymentTerminals.Add(new TenantPaymentTerminal
                    {
                        TenantPaymentTerminalId = 3, TenantId = 1, Provider = "sumup", ProviderTerminalId = "rdr_1",
                        DisplayName = "Solo 1", Enabled = true, ConfigurationJson = """{"tipRates":"5,10"}""",
                        Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                    });
                    if (withAccess)
                    {
                        var access = new TenantProviderAccess
                        {
                            TenantId = 1, Provider = "sumup", ConnectionName = "payments-sumup-1", ProviderAccountId = "MC123",
                            Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                        };
                        db.TenantProviderAccesses.Add(access);
                        await db.SaveChangesAsync();
                        accessId = access.TenantProviderAccessId;
                    }

                    await db.SaveChangesAsync();
                }

                var http = new FakeSumUp();
                var api = new SumUpApiClient(new HttpClient(http), new StaticSettings<SumUpOptions>(new SumUpOptions
                {
                    ApiBaseUrl = "https://api.sumup.invalid",
                    AffiliateAppId = "app-1",
                    AffiliateKey = "aff-key",
                    WebhookUrl = "https://shop.invalid/sumup"
                }));
                var credentials = new FixedCredentials();
                var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
                return new Env
                {
                    Factory = factory,
                    Http = http,
                    Api = api,
                    Credentials = credentials,
                    AccessId = accessId,
                    Service = new SumUpTerminalPaymentService<PaymentsTestContext>(factory, api, credentials,
                        PaymentSettings(), [new OpenGate()], sink)
                };
            }

            public static StaticSettings<TenantPaymentsOptions> PaymentSettings() => new(new TenantPaymentsOptions
            {
                Enabled = true,
                DefaultCurrency = "CHF"
            });

            public static TerminalSaleRequest Request(decimal amount) => new()
            {
                TenantId = 1,
                TerminalId = 3,
                ExternalReference = "bon-1",
                Description = "Kaffee",
                Amount = amount,
                Currency = "CHF"
            };
        }

        /// <summary>Ein SumUp, das antwortet, was der Test vorgibt, und mitschreibt, was ankam.</summary>
        private sealed class FakeSumUp : HttpMessageHandler
        {
            private readonly Dictionary<string, (int Status, string Body)> answers = new();

            public List<(string Method, string Path, string Query, string? Authorization, string? Body)> Calls { get; } = new();

            public void On(string method, string path, int status, string body) => answers[$"{method} {path}"] = (status, body);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri!.AbsolutePath;
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Calls.Add((request.Method.Method, path, request.RequestUri.Query, request.Headers.Authorization?.ToString(), body));
                var (status, text) = answers.TryGetValue($"{request.Method.Method} {path}", out var answer)
                    ? answer
                    : (404, """{"error_code":"NOT_FOUND"}""");
                return new HttpResponseMessage((HttpStatusCode)status)
                {
                    Content = new StringContent(text, Encoding.UTF8, "application/json")
                };
            }
        }

        private sealed class FixedCredentials : IProviderCredentialStore
        {
            public Task<ProviderCredential> GetCredentialAsync(int tenantId, string connectionName, CancellationToken cancellationToken = default)
                => Task.FromResult(new ProviderCredential(ExternalServiceAuthenticationType.BearerToken, null, "sup_sk_test_tenant"));

            public Task SaveSecretAsync(int tenantId, string connectionName, ExternalServiceAuthenticationType authenticationType,
                string? clientId, string secret, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed class OpenGate : IPaymentFeatureGate
        {
            public Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }

        private sealed class NoSales : ITenantSaleService
        {
            public Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason, bool? refundApplicationFee = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<SaleResult> CancelSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class NoAccounts : ITenantPaymentAccountService
        {
            public Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl, string? email = null, string? country = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
    }
}
