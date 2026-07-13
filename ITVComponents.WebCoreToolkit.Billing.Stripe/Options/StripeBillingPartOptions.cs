namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Options
{
    /// <summary>
    /// WebPart activation options for the Stripe billing service layer. Bound from the part's own
    /// configuration section; drives whether the part activates, which host billing context it binds to,
    /// where the Stripe secrets are read from and under which path the webhook endpoint is mapped.
    /// </summary>
    public class StripeBillingPartOptions
    {
        /// <summary>When <c>true</c>, the part registers the Stripe services and maps the webhook endpoint.</summary>
        public bool ActivateStripeBilling { get; set; }

        /// <summary>
        /// Type expression of the host billing context (must be a <c>DbContext</c> implementing
        /// <c>IBillingContext</c>) the generic <c>AddStripeBilling&lt;TContext&gt;</c> is closed over.
        /// </summary>
        public string? ContextType { get; set; }

        /// <summary>
        /// Configuration section the Stripe secrets (<see cref="StripeOptions"/>) are read from. Not
        /// hard-coded — specify it per host in the WebPart config. Defaults to <c>Billing:Stripe</c>.
        /// </summary>
        public string StripeConfigPath { get; set; } = "Billing:Stripe";

        /// <summary>Path the Stripe webhook receiver is mapped to. Defaults to <c>/billing/webhook</c>.</summary>
        public string WebhookPath { get; set; } = "/billing/webhook";

        /// <summary>
        /// Stripe secrets resolved from <see cref="StripeConfigPath"/> at config-load time (not bound from the
        /// part section itself). Populated by the WebPart's config loader.
        /// </summary>
        public StripeOptions? Stripe { get; set; } = null;
    }
}
