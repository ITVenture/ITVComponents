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

        /// <summary>
        /// When <c>true</c>, the part additionally registers the Connect service layer (axis B: the tenant's own
        /// end customers pay the tenant) and maps the connect webhook plus the onboarding return endpoints. This
        /// is the DEPLOYMENT switch — off means neither services nor endpoints exist, whatever a tenant may be
        /// entitled to.
        /// </summary>
        public bool ActivatePayments { get; set; }

        /// <summary>
        /// Type expression of the context hosting the payments tables (a <c>DbContext</c> implementing
        /// <c>IPaymentsContext</c>). Null uses <see cref="ContextType"/> — the usual case, where one host context
        /// carries both axes.
        /// </summary>
        public string? PaymentsContextType { get; set; }

        /// <summary>Path the connect webhook receiver is mapped to. A SEPARATE endpoint with its own secret.</summary>
        public string ConnectWebhookPath { get; set; } = "/billing/connect/webhook";

        /// <summary>Path the provider returns the tenant to after the hosted onboarding.</summary>
        public string ConnectReturnPath { get; set; } = "/billing/connect/return";

        /// <summary>Path the provider calls when an onboarding link has expired.</summary>
        public string ConnectRefreshPath { get; set; } = "/billing/connect/refresh";

        /// <summary>The payout-account page both return endpoints redirect to.</summary>
        public string PaymentsManagePath { get; set; } = "/Account/Manage/Payments";

        /// <summary>
        /// When <c>true</c>, the volume-based waiver of the subscription base fee is registered. Requires the
        /// payments context to implement <c>IBillingContext</c> as well — the waiver is the one place where the
        /// two axes meet.
        /// </summary>
        public bool ActivateVolumeWaiver { get; set; }
    }
}
