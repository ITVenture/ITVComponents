using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options
{
    /// <summary>
    /// Provider-agnostic billing configuration consumed by the Stripe (or future) service-layer libraries.
    /// Provider-specific keys live in the implementing lib's own option section (e.g. <c>Billing:Stripe</c>).
    /// </summary>
    [SettingName("Billing")]
    public class BillingProviderOptions
    {
        /// <summary>
        /// Whether the billing module is enabled. When false, the service layer should short-circuit
        /// checkout / webhook handling and the UI should hide subscription-management pages.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Optional URL path the customer is redirected to after Stripe checkout succeeds.
        /// </summary>
        public string SuccessReturnUrl { get; set; }

        /// <summary>
        /// Optional URL path the customer is redirected to after Stripe checkout is cancelled.
        /// </summary>
        public string CancelReturnUrl { get; set; }

        /// <summary>
        /// Optional URL path the customer is redirected to after Stripe customer-portal closes.
        /// </summary>
        public string PortalReturnUrl { get; set; }
    }
}
