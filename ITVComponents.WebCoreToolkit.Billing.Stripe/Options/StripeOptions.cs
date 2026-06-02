using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Options
{
    /// <summary>
    /// Stripe-specific secrets and settings. Bind from configuration section <c>Billing:Stripe</c>
    /// (user-secrets in dev, KeyVault / env-vars in prod).
    /// </summary>
    [SettingName("Billing:Stripe")]
    public class StripeOptions
    {
        /// <summary>Stripe secret API key (<c>sk_...</c>).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Signing secret of the Stripe webhook endpoint (<c>whsec_...</c>), used to verify events.</summary>
        public string WebhookSecret { get; set; } = string.Empty;
    }
}
