using System.Text.Json;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Impl
{
    /// <summary>
    /// A correctly signed webhook event whose API version belongs to a different release line than the one
    /// this Stripe.net package deserializes.
    /// </summary>
    /// <remarks>
    /// A type of its own because the cure differs from a signature failure: the secret is fine, the webhook
    /// endpoint has to be recreated on the expected version.
    /// </remarks>
    public sealed class StripeWebhookApiVersionException : StripeException
    {
        public StripeWebhookApiVersionException(string? receivedVersion, string expectedVersion)
            : base($"The event has API version '{receivedVersion ?? "(none)"}', but this Stripe.net package expects "
                   + $"'{expectedVersion}'. The signature is valid, so the secret is not the problem: the webhook "
                   + "endpoint has to deliver the expected version. Create a new endpoint with that version, or "
                   + "locally use 'stripe listen --latest'.")
        {
            ReceivedVersion = receivedVersion;
            ExpectedVersion = expectedVersion;
        }

        /// <summary>The version the event carries; null when it carries none.</summary>
        public string? ReceivedVersion { get; }

        /// <summary>The version the installed Stripe.net package is pinned to.</summary>
        public string ExpectedVersion { get; }
    }

    /// <summary>
    /// Turns a received webhook into an event: signature first, then the API version.
    /// </summary>
    public static class StripeWebhookEvents
    {
        /// <summary>
        /// Verifies a v1 webhook and reads its event.
        /// </summary>
        /// <param name="payload">the raw request body</param>
        /// <param name="signatureHeader">the value of the <c>Stripe-Signature</c> header</param>
        /// <param name="secret">the signing secret of the receiving endpoint</param>
        /// <returns>the event</returns>
        /// <exception cref="StripeWebhookApiVersionException">the signature is valid, the version does not fit</exception>
        /// <exception cref="StripeException">the signature is not valid</exception>
        /// <remarks>
        /// There is deliberately no way to accept another release line. Stripe.net would read the object anyway
        /// and may read it wrong, and on this path that means a wrongly booked sale. A rejected event is
        /// answered with 400, so Stripe delivers it again.
        /// </remarks>
        public static Event Construct(string payload, string signatureHeader, string secret)
        {
            EventUtility.ValidateSignature(payload, signatureHeader, secret);

            string? version = ReadApiVersion(payload);
            if (version == null || !EventUtility.IsCompatibleApiVersion(version))
            {
                throw new StripeWebhookApiVersionException(version, StripeConfiguration.ApiVersion);
            }

            return EventUtility.ParseEvent(payload);
        }

        /// <summary>
        /// Read from the raw text: an event of another release line is exactly the one that must not go through
        /// the deserializer before its version has been looked at.
        /// </summary>
        private static string? ReadApiVersion(string payload)
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("api_version", out JsonElement version)
                   && version.ValueKind == JsonValueKind.String
                ? version.GetString()
                : null;
        }
    }
}
