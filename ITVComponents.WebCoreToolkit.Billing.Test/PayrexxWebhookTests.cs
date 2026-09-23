using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Die Payrexx-Seite der Zahlungsmeldungen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nicht geprüft wird, ob Payrexx die Felder so schickt, wie sie hier stehen — das braucht ein echtes
    /// Konto. Geprüft wird alles, was danach kommt und in unserer Hand liegt: ob die Signatur überhaupt
    /// greift, ob beide Rumpf-Formate gelesen werden, und vor allem, ob aus einer bestätigten
    /// <b>Erstattung</b> versehentlich eine bestätigte <b>Zahlung</b> wird.
    /// </para>
    /// <para>
    /// Die Buchung selbst ist nicht nochmals Gegenstand — die steht in
    /// <see cref="TenantSaleWebhookSinkTests"/>. Hier geht es um die Übersetzung davor.
    /// </para>
    /// </remarks>
    [TestClass]
    public class PayrexxWebhookTests
    {
        /// <summary>Ein Geheimnis, das GÜLTIGES Base64 ist — sonst liesse sich die Falle unten nicht stellen.</summary>
        private const string Secret = "c2VjcmV0LWtleQ==";

        [TestMethod]
        public async Task Confirmed_ReleasesTheSale()
        {
            var (handler, factory) = Build(Secret);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1", email: "buyer@example.com", paymentMean: "twint");

            await handler.HandleAsync(body, Sign(body, Secret), "application/json", default);

            var sale = await Single(factory);
            Assert.AreEqual(TenantSaleStatus.Paid, sale.Status);
            Assert.AreEqual("txn-1", sale.ProviderChargeId, "Erstattet wird später gegen die UUID, nicht gegen die Nummer.");
            Assert.AreEqual("buyer@example.com", sale.CustomerEmail);
        }

        [TestMethod]
        public async Task Confirmed_AcceptsAnUpperCaseSignature()
        {
            // Die Prüfung kleinschreibt, was hereinkommt. Wer den HMAC anderswo gross erzeugt, soll nicht
            // an dieser Stelle scheitern - das wäre ein Fehler, der wie ein falscher Schlüssel aussieht.
            var (handler, factory) = Build(Secret);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1");

            await handler.HandleAsync(body, Sign(body, Secret).ToUpperInvariant(), "application/json", default);

            Assert.AreEqual(TenantSaleStatus.Paid, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task Signature_RejectsTheTwoWaysItIsUsuallyGotWrong()
        {
            // Beides sind Fehler, die nicht als Fehler aussehen: die Prüfung schlägt einfach immer fehl,
            // und die Suche beginnt beim hinterlegten Schlüssel statt beim Verfahren.
            var (handler, factory) = Build(Secret);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1");

            // 1. Der Schlüssel als Base64 DEKODIERT statt als UTF-8-Text genommen.
            var decodedKey = Convert.ToHexStringLower(HMACSHA256.HashData(
                Convert.FromBase64String(Secret), Encoding.UTF8.GetBytes(body)));
            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => handler.HandleAsync(body, decodedKey, "application/json", default));

            // 2. Das Ergebnis als Base64 statt als kleingeschriebenes Hex.
            var base64Digest = Convert.ToBase64String(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body)));
            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => handler.HandleAsync(body, base64Digest, "application/json", default));

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task Signature_OverTheRawBodyOnly()
        {
            // Die Signatur läuft über genau die Zeichen, die ankamen. Ein neu zusammengesetzter Rumpf
            // - und sei es nur ein Leerzeichen mehr - ergibt eine andere.
            var (handler, factory) = Build(Secret);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1");

            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => handler.HandleAsync(body + " ", Sign(body, Secret), "application/json", default));

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task Signature_MissingHeaderIsRejectedWhenASecretIsConfigured()
        {
            var (handler, factory) = Build(Secret);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1");

            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => handler.HandleAsync(body, null, "application/json", default));

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task Signature_WithoutASecretTheNotificationIsAcceptedUnchecked()
        {
            // Bewusst so: Payrexx führt die Signierung als Wahlmöglichkeit, und ein Betrieb ohne sie soll
            // nicht stillstehen. Der Weg protokolliert das jedes Mal - hier festgehalten ist nur, dass er
            // nicht ablehnt.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            Assert.AreEqual(TenantSaleStatus.Paid, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task EmptyBody_IsRejected()
        {
            var (handler, _) = Build(secret: string.Empty);

            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => handler.HandleAsync(string.Empty, null, "application/json", default));
        }

        [TestMethod]
        public async Task UnreadableJson_IsRejected()
        {
            // Abgelehnt, nicht bestätigt: Payrexx wiederholt bis zu zehnmal. Eine angenommene und dann
            // verworfene Meldung wäre endgültig weg.
            var (handler, _) = Build(secret: string.Empty);

            await Assert.ThrowsExactlyAsync<PayrexxWebhookRejectedException>(
                () => handler.HandleAsync("{\"transaction\":", null, "application/json", default));
        }

        [TestMethod]
        public async Task NotificationWithoutTransaction_IsAcknowledged()
        {
            // Auszahlungsmeldungen sehen so aus. Sie abzulehnen hiesse, Payrexx tagelang etwas wiederholen
            // zu lassen, das nie anders ausgeht.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);

            await handler.HandleAsync("{\"payout\":{\"id\":7}}", null, "application/json", default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task UnknownSale_IsAcknowledgedNotRejected()
        {
            // Eine Zahlung, die zu keinem Verkauf von UNS gehört, gehört auch beim zehnten Versuch zu
            // keinem. Bestätigen statt wiederholen lassen.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1", paymentRequestId: 999, reference: "fremd");

            await handler.HandleAsync(body, null, "application/json", default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task FormEncodedBody_IsReadLikeJson()
        {
            // Die Einstellung "Normal (PHP-Post)" im Portal. Eine falsch eingestellte Instanz soll nicht
            // dazu führen, dass Zahlungen unbemerkt liegen bleiben.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = "transaction[id]=42&transaction[uuid]=txn-1&transaction[status]=confirmed"
                       + "&transaction[amount]=5000&transaction[currency]=CHF"
                       + "&transaction[invoice][paymentRequestId]=777"
                       + "&transaction[contact][email]=buyer%40example.com";

            await handler.HandleAsync(body, null, "application/x-www-form-urlencoded", default);

            var sale = await Single(factory);
            Assert.AreEqual(TenantSaleStatus.Paid, sale.Status);
            Assert.AreEqual("txn-1", sale.ProviderChargeId);
            Assert.AreEqual("buyer@example.com", sale.CustomerEmail, "Die Adresse muss die Entschlüsselung des Schlüssels überstehen.");
        }

        [TestMethod]
        public async Task JsonIsRecognisedByItsContent_NotOnlyByItsContentType()
        {
            // Der Inhaltstyp fehlt bei manchen Vermittlern oder ist falsch. Ein '{' am Anfang ist die
            // verlässlichere Aussage - und ohne diese Regel würde JSON durch den Formular-Leser laufen
            // und lautlos als "keine Transaktion" enden.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("confirmed", uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/x-www-form-urlencoded", default);

            Assert.AreEqual(TenantSaleStatus.Paid, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task ConfirmedRefundTransaction_IsNotAPayment()
        {
            // DIE teuerste Verwechslung in dieser Datei. Payrexx führt eine Erstattung als EIGENE
            // Transaktion - mit eigener UUID und mit Zustand 'confirmed'. Wer die als Zahlung durchlässt,
            // gibt die Ware frei, weil Geld ZURÜCKgegangen ist.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("confirmed", uuid: "refund-1", originalTransactionUuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            var sale = await Single(factory);
            Assert.AreEqual(TenantSaleStatus.Pending, sale.Status, "Eine bestätigte Erstattung ist keine bestätigte Zahlung.");
            Assert.IsNull(sale.ProviderChargeId, "Und ihre eigene UUID darf nicht als Zahlungs-Kennung stehenbleiben.");
        }

        [TestMethod]
        public async Task Refunded_PaysFirstAndThenMirrorsTheTotal()
        {
            // Meldungen überholen sich: ging die Bestätigung verloren und folgte die Erstattung schnell,
            // trägt DIESE Meldung beides. Andersherum stünde eine Erstattung auf einem offenen Verkauf.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("refunded", uuid: "txn-1", refundedAmount: 5000);

            await handler.HandleAsync(body, null, "application/json", default);

            var sale = await Single(factory, withRefunds: true);
            Assert.AreEqual(TenantSaleStatus.Refunded, sale.Status);
            Assert.AreEqual("txn-1", sale.ProviderChargeId);
            Assert.AreEqual(5000, sale.Refunds.Sum(r => r.AmountMinor));
        }

        [TestMethod]
        public async Task PartiallyRefunded_BooksTheTotalOnceEvenWhenDeliveredTwice()
        {
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("partially-refunded", uuid: "txn-1", refundedAmount: 2000);

            await handler.HandleAsync(body, null, "application/json", default);
            await handler.HandleAsync(body, null, "application/json", default);

            var sale = await Single(factory, withRefunds: true);
            Assert.AreEqual(TenantSaleStatus.PartiallyRefunded, sale.Status);
            Assert.AreEqual(1, sale.Refunds.Count, "Derselbe Gesamtstand ist dieselbe Erstattung.");
            Assert.AreEqual(2000, sale.Refunds.Sum(r => r.AmountMinor));
        }

        [TestMethod]
        public async Task Refunded_WithoutAnAmountBooksNothing()
        {
            // Der Gesamtstand IST der Inhalt der Meldung. Fehlt er, wird nicht geraten - es bliebe nur,
            // den vollen Betrag anzunehmen, und das wäre bei einer Teilerstattung falsch.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("refunded", uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            var sale = await Single(factory, withRefunds: true);
            Assert.AreEqual(TenantSaleStatus.Paid, sale.Status, "Bezahlt ist sie - nur erstattet ist nichts.");
            Assert.AreEqual(0, sale.Refunds.Count);
        }

        [TestMethod]
        [DataRow("cancelled", TenantSaleStatus.Canceled)]
        [DataRow("expired", TenantSaleStatus.Expired)]
        [DataRow("declined", TenantSaleStatus.Failed)]
        [DataRow("error", TenantSaleStatus.Failed)]
        public async Task TheEndStatesMapToTheirOwn(string status, TenantSaleStatus expected)
        {
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json(status, uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            Assert.AreEqual(expected, (await Single(factory)).Status);
        }

        [TestMethod]
        [DataRow("waiting")]
        [DataRow("authorized")]
        [DataRow("reserved")]
        [DataRow("refund_pending")]
        public async Task TheIntermediateStatesBookNothing(string status)
        {
            // 'authorized' ist der gefährliche darunter: da ist der Betrag reserviert, aber nicht
            // eingezogen. Wer hier freigäbe, lieferte gegen ein Versprechen.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json(status, uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        [DataRow("chargeback")]
        [DataRow("disputed")]
        public async Task ARecallLeavesTheSaleUntouched(string status)
        {
            // Eine Rückbelastung ist KEINE Erstattung: das Geld ist weg, die Ware ist draussen, und was
            // zu tun ist, entscheidet ein Mensch. Der Verkauf bleibt darum, wie er ist.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory, paid: true);
            var body = Json(status, uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            var sale = await Single(factory, withRefunds: true);
            Assert.AreEqual(TenantSaleStatus.Paid, sale.Status);
            Assert.AreEqual(0, sale.Refunds.Count);
        }

        [TestMethod]
        public async Task AnUnknownStatusBooksNothing()
        {
            // Die Liste gehört Payrexx und wächst. Geraten wird nicht.
            var (handler, factory) = Build(secret: string.Empty);
            await Seed(factory);
            var body = Json("beglaubigt-vielleicht", uuid: "txn-1");

            await handler.HandleAsync(body, null, "application/json", default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        /// <summary>Der Weg samt eigener Ablage — jeder Test bekommt seine eigene.</summary>
        private static (PayrexxWebhookHandler<PaymentsTestContext> Handler,
            IDbContextFactory<PaymentsTestContext> Factory) Build(string secret)
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
            var handler = new PayrexxWebhookHandler<PaymentsTestContext>(sink,
                new StaticSettings<PayrexxOptions>(new PayrexxOptions { WebhookSecret = secret }));
            return (handler, factory);
        }

        /// <summary>Der HMAC, wie Payrexx ihn schickt: kleingeschriebenes Hex, Schlüssel als UTF-8-Text.</summary>
        private static string Sign(string body, string secret)
            => Convert.ToHexStringLower(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

        /// <summary>Eine Meldung im JSON-Format.</summary>
        private static string Json(string status, string uuid, long paymentRequestId = 777,
            string reference = "order-1", string? email = null, string? paymentMean = null,
            string? originalTransactionUuid = null, long? refundedAmount = null)
        {
            var invoice = $"\"paymentRequestId\":{paymentRequestId},\"referenceId\":\"{reference}\""
                          + (refundedAmount != null ? $",\"refundedAmount\":{refundedAmount}" : string.Empty);
            var extra = (email != null ? $",\"contact\":{{\"email\":\"{email}\"}}" : string.Empty)
                        + (paymentMean != null ? $",\"paymentMean\":\"{paymentMean}\"" : string.Empty)
                        + (originalTransactionUuid != null
                            ? $",\"originalTransactionUuid\":\"{originalTransactionUuid}\""
                            : string.Empty);

            return $"{{\"transaction\":{{\"id\":42,\"uuid\":\"{uuid}\",\"status\":\"{status}\","
                   + $"\"referenceId\":\"{reference}\",\"amount\":5000,\"currency\":\"CHF\","
                   + $"\"invoice\":{{{invoice}}}{extra}}}}}";
        }

        /// <summary>Ein Verkauf, dessen Zahlungsseite die Nummer 777 trägt.</summary>
        private static async Task Seed(IDbContextFactory<PaymentsTestContext> factory, bool paid = false)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.TenantSales.Add(new TenantSale
            {
                TenantId = 1,
                ExternalReference = "order-1",
                Description = "test",
                AmountMinor = 5000,
                Currency = "CHF",
                Status = paid ? TenantSaleStatus.Paid : TenantSaleStatus.Pending,
                ProviderSessionId = "777",
                ProviderChargeId = paid ? "txn-1" : null,
                CheckoutUrl = paid ? null : "https://example.invalid/pay",
                Provider = "payrexx",
                Created = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private static async Task<TenantSale> Single(IDbContextFactory<PaymentsTestContext> factory,
            bool withRefunds = false)
        {
            await using var db = await factory.CreateDbContextAsync();
            IQueryable<TenantSale> query = withRefunds ? db.TenantSales.Include(s => s.Refunds) : db.TenantSales;
            return await query.SingleAsync();
        }
    }
}
