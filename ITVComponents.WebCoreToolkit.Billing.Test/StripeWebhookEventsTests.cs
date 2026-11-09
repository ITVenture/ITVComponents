using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Vom eingehenden Webhook zum Ereignis: zuerst die Signatur, dann die API-Version.
    /// </summary>
    /// <remarks>
    /// Beide Fehler enden mit 400, aber die Abhilfe ist eine andere. Eine falsche Version darf deshalb nicht wie
    /// ein falsches Secret aussehen, und ein falsches Secret nicht wie eine falsche Version.
    /// </remarks>
    [TestClass]
    public class StripeWebhookEventsTests
    {
        private const string Secret = "whsec_test";

        /// <summary>Eine fremde Linie, abgeleitet aus der aktuellen, damit der Test den naechsten Bump uebersteht.</summary>
        private static string ForeignVersion => CurrentLine == "dahlia" ? "2026-05-27.acacia" : "2026-05-27.dahlia";

        private static string CurrentLine => StripeConfiguration.ApiVersion.Split('.')[1];

        private static string Payload(string? apiVersion)
        {
            string version = apiVersion == null ? string.Empty : $"\"api_version\":\"{apiVersion}\",";
            return "{\"id\":\"evt_1\",\"object\":\"event\"," + version
                   + "\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_1\",\"object\":\"charge\"}}}";
        }

        private static string Sign(string payload, string secret = Secret)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
            return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        [TestMethod]
        public void TheVersionOfThePackage_IsAccepted()
        {
            string payload = Payload(StripeConfiguration.ApiVersion);

            Event stripeEvent = StripeWebhookEvents.Construct(payload, Sign(payload), Secret);

            Assert.AreEqual("evt_1", stripeEvent.Id);
            Assert.AreEqual(EventTypes.ChargeRefunded, stripeEvent.Type);
        }

        /// <summary>Innerhalb einer Linie gibt es keine Bruchstellen, ein anderes Datum ist also in Ordnung.</summary>
        [TestMethod]
        public void AnotherDateOfTheSameLine_IsAccepted()
        {
            string payload = Payload($"2020-01-01.{CurrentLine}");

            Assert.AreEqual("evt_1", StripeWebhookEvents.Construct(payload, Sign(payload), Secret).Id);
        }

        [TestMethod]
        public void AnotherLine_IsRejectedAndNamesBothVersions()
        {
            string payload = Payload(ForeignVersion);

            var ex = Assert.ThrowsExactly<StripeWebhookApiVersionException>(
                () => StripeWebhookEvents.Construct(payload, Sign(payload), Secret));

            Assert.AreEqual(ForeignVersion, ex.ReceivedVersion);
            Assert.AreEqual(StripeConfiguration.ApiVersion, ex.ExpectedVersion);
            StringAssert.Contains(ex.Message, ForeignVersion);
            StringAssert.Contains(ex.Message, StripeConfiguration.ApiVersion);
        }

        [TestMethod]
        public void AnEventWithoutVersion_IsRejected()
        {
            string payload = Payload(null);

            var ex = Assert.ThrowsExactly<StripeWebhookApiVersionException>(
                () => StripeWebhookEvents.Construct(payload, Sign(payload), Secret));

            Assert.IsNull(ex.ReceivedVersion);
        }

        /// <summary>
        /// Stimmen beide nicht, zaehlt die Signatur: wer das Secret nicht kennt, erfaehrt nichts ueber die Version,
        /// und die Meldung schickt niemanden zum falschen Endpunkt.
        /// </summary>
        [TestMethod]
        public void AWrongSignature_IsReportedAsSuchEvenWithAForeignVersion()
        {
            string payload = Payload(ForeignVersion);

            var ex = Assert.ThrowsExactly<StripeException>(
                () => StripeWebhookEvents.Construct(payload, Sign(payload, "whsec_other"), Secret));

            Assert.IsNotInstanceOfType<StripeWebhookApiVersionException>(ex);
        }

        [TestMethod]
        public void AChangedBody_IsRejectedBeforeItsVersionIsRead()
        {
            string signed = Payload(StripeConfiguration.ApiVersion);

            var ex = Assert.ThrowsExactly<StripeException>(
                () => StripeWebhookEvents.Construct("not json", Sign(signed), Secret));

            Assert.IsNotInstanceOfType<StripeWebhookApiVersionException>(ex);
        }
    }
}
