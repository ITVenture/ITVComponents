using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Impl;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Die wallee-Seite der Zustandsmeldungen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// wallee schickt keine Daten, sondern nur eine Kennung, und erwartet, dass die Entität danach über
    /// die API gelesen wird. Dieser Rückruf ist ohne echten Raum nicht zu prüfen — <b>ausser</b> auf dem
    /// Weg, auf dem es ihn gar nicht braucht: ist am Listener „payload signing and state" eingeschaltet,
    /// kommt der Zustand mit, und die Transaktions-Kennung IST, was beim Anlegen gespeichert wurde.
    /// Genau dieser Weg steht hier, und er ist der, den ein Betrieb einschalten soll.
    /// </para>
    /// <para>
    /// Die Erstattungs-Seite fehlt darum bewusst: sie liest IMMER zurück. Was von ihr prüfbar ist — die
    /// Spiegelung einer einzelnen Erstattung — steht in <see cref="TenantSaleWebhookSinkTests"/>.
    /// </para>
    /// </remarks>
    [TestClass]
    public class WalleeWebhookTests
    {
        [TestMethod]
        [DataRow("COMPLETED")]
        [DataRow("FULFILL")]
        public async Task ThePaidStatesReleaseTheSale(string state)
        {
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, state), null, default);

            var sale = await Single(factory);
            Assert.AreEqual(TenantSaleStatus.Paid, sale.Status);
            Assert.AreEqual("4711", sale.ProviderChargeId,
                "Erstattet wird bei wallee gegen die TRANSAKTION, nicht gegen eine eigene Zahlungs-Kennung.");
        }

        [TestMethod]
        public async Task ARepeatedNotificationReleasesOnlyOnce()
        {
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, "COMPLETED"), null, default);
            await handler.HandleAsync(Notice("Transaction", 4711, "COMPLETED"), null, default);

            Assert.AreEqual(TenantSaleStatus.Paid, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task Authorized_DoesNotRelease()
        {
            // Bei AUTHORIZED ist der Betrag reserviert, aber nicht eingezogen. Wer hier freigäbe,
            // lieferte gegen ein Versprechen - und der Name des Zustands lädt genau dazu ein.
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, "AUTHORIZED"), null, default);

            var sale = await Single(factory);
            Assert.AreEqual(TenantSaleStatus.Pending, sale.Status);
            Assert.IsNull(sale.ProviderChargeId);
        }

        [TestMethod]
        [DataRow("CREATE")]
        [DataRow("PENDING")]
        [DataRow("CONFIRMED")]
        [DataRow("PROCESSING")]
        public async Task TheIntermediateStatesBookNothing(string state)
        {
            // 'CONFIRMED' heisst bei wallee NICHT bezahlt - anders als bei Payrexx, wo genau dieses Wort
            // die Zahlung ist. Dieselbe Silbe, zwei Bedeutungen.
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, state), null, default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        [DataRow("FAILED", TenantSaleStatus.Failed)]
        [DataRow("DECLINE", TenantSaleStatus.Failed)]
        [DataRow("VOIDED", TenantSaleStatus.Canceled)]
        public async Task TheEndStatesMapToTheirOwn(string state, TenantSaleStatus expected)
        {
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, state), null, default);

            Assert.AreEqual(expected, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task AnUnknownStateBooksNothing()
        {
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, "SCHWEBEND"), null, default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task AListenerOnSomethingElse_IsAcknowledged()
        {
            // Ein Listener auf einer anderen Entitätsart ist kein Fehler. Eine Absage liesse wallee
            // wiederholen, was hier nie anders ausgeht - und der Rückruf würde nicht einmal versucht.
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Subscription", 4711, "ACTIVE"), null, default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task AnUnknownTransaction_IsAcknowledgedNotRejected()
        {
            var (handler, factory) = Build();
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 9999, "COMPLETED"), null, default);

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task EmptyBody_IsRejected()
        {
            var (handler, _) = Build();

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(
                () => handler.HandleAsync(string.Empty, null, default));
        }

        [TestMethod]
        public async Task UnreadableBody_IsRejected()
        {
            var (handler, _) = Build();

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(
                () => handler.HandleAsync("{\"entityId\":", null, default));
        }

        [TestMethod]
        public async Task ANoticeWithoutAnEntityId_IsRejected()
        {
            // Ohne Kennung gibt es nichts zu tun - und nichts, worüber sich der Verkauf finden liesse.
            var (handler, _) = Build();

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(
                () => handler.HandleAsync("{\"listenerEntityTechnicalName\":\"Transaction\"}", null, default));
        }

        [TestMethod]
        public async Task WithoutASpace_TheNoticeIsRejected()
        {
            // Der Raum ist die Klammer um alles bei wallee. Fehlt er in der Meldung UND in den
            // Einstellungen, gibt es keinen Weg, die genannte Entität zu lesen.
            var (handler, _) = Build(spaceId: 0);

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(
                () => handler.HandleAsync(Notice("Transaction", 4711, "COMPLETED", spaceId: 0), null, default));
        }

        [TestMethod]
        public async Task TheSpaceFromTheNoticeStandsInForTheConfiguredOne()
        {
            var (handler, factory) = Build(spaceId: 0);
            await Seed(factory);

            await handler.HandleAsync(Notice("Transaction", 4711, "COMPLETED", spaceId: 55), null, default);

            Assert.AreEqual(TenantSaleStatus.Paid, (await Single(factory)).Status);
        }

        [TestMethod]
        public async Task WithVerificationOn_AMissingSignatureHeaderIsRejected()
        {
            // Der Endpunkt ist öffentlich. Ohne Prüfung könnte jeder, der ihn kennt, beliebige Verkäufe
            // als bezahlt melden - darum ist eine fehlende Kopfzeile eine Absage und kein Durchwinken.
            var (handler, factory) = Build(verifySignatures: true);
            await Seed(factory);

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(
                () => handler.HandleAsync(Notice("Transaction", 4711, "COMPLETED"), null, default));

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        [TestMethod]
        [DataRow("algorithm=SHA256withECDSA")]
        [DataRow("algorithm=SHA256withECDSA, keyId=abc")]
        [DataRow("algorithm=SHA256withECDSA, signature=AAAA==")]
        [DataRow("irgendwas")]
        public async Task AMalformedSignatureHeaderIsRejectedBeforeAnythingIsFetched(string header)
        {
            // Wichtig ist nicht nur DASS abgelehnt wird, sondern WANN: die Absage fällt, bevor der
            // öffentliche Schlüssel geholt wird. Dieser Test läuft ohne Netz - käme er bis zum Abholen,
            // schlüge er mit einem ganz anderen Fehler fehl.
            var (handler, factory) = Build(verifySignatures: true);
            await Seed(factory);

            await Assert.ThrowsExactlyAsync<WalleeWebhookRejectedException>(
                () => handler.HandleAsync(Notice("Transaction", 4711, "COMPLETED"), header, default));

            Assert.AreEqual(TenantSaleStatus.Pending, (await Single(factory)).Status);
        }

        /// <summary>Der Weg samt eigener Ablage — jeder Test bekommt seine eigene.</summary>
        /// <remarks>
        /// Die Signaturprüfung ist in der Vorgabe AUS: sie holt den öffentlichen Schlüssel bei wallee, und
        /// das wäre ein Netzaufruf. Wo sie Gegenstand ist, schaltet der Test sie ausdrücklich ein.
        /// </remarks>
        private static (WalleeWebhookHandler<PaymentsTestContext> Handler,
            IDbContextFactory<PaymentsTestContext> Factory) Build(long spaceId = 55,
            bool verifySignatures = false)
        {
            var factory = new PaymentsTestContextFactory(Guid.NewGuid().ToString());
            var sink = new TenantSaleWebhookSink<PaymentsTestContext>(factory, new TenantSaleNotifier([]));
            var handler = new WalleeWebhookHandler<PaymentsTestContext>(sink,
                new StaticSettings<WalleeOptions>(new WalleeOptions
                {
                    SpaceId = spaceId,
                    VerifyWebhookSignatures = verifySignatures
                }));
            return (handler, factory);
        }

        /// <summary>Eine Meldung, wie sie bei eingeschaltetem „payload signing and state" ankommt.</summary>
        private static string Notice(string entityName, long entityId, string state, long spaceId = 55)
            => $"{{\"eventId\":1,\"entityId\":{entityId},\"listenerEntityTechnicalName\":\"{entityName}\","
               + $"\"spaceId\":{spaceId},\"webhookListenerId\":9,\"state\":\"{state}\"}}";

        /// <summary>Ein Verkauf, dessen Transaktion bei wallee die Nummer 4711 trägt.</summary>
        private static async Task Seed(IDbContextFactory<PaymentsTestContext> factory)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.TenantSales.Add(new TenantSale
            {
                TenantId = 1,
                ExternalReference = "order-1",
                Description = "test",
                AmountMinor = 5000,
                Currency = "CHF",
                Status = TenantSaleStatus.Pending,
                ProviderSessionId = "4711",
                CheckoutUrl = "https://example.invalid/pay",
                Provider = "wallee",
                Created = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private static async Task<TenantSale> Single(IDbContextFactory<PaymentsTestContext> factory)
        {
            await using var db = await factory.CreateDbContextAsync();
            return await db.TenantSales.SingleAsync();
        }
    }
}
