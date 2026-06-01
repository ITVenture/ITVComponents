using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options
{
    /// <summary>
    /// Provider-agnostic billing configuration consumed by the service-layer libraries (Stripe etc.).
    /// Provider-specific secrets live in the implementing lib's own option section (e.g. <c>Billing:Stripe</c>).
    /// </summary>
    [SettingName("Billing")]
    public class BillingProviderOptions
    {
        /// <summary>
        /// Whether the billing module is enabled. When false the service layer should short-circuit
        /// checkout / webhook handling and the UI should hide subscription-management pages.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>URL the customer returns to after a successful checkout.</summary>
        public string? SuccessReturnUrl { get; set; }

        /// <summary>URL the customer returns to after a cancelled checkout.</summary>
        public string? CancelReturnUrl { get; set; }

        /// <summary>URL the customer returns to after the provider customer-portal closes.</summary>
        public string? PortalReturnUrl { get; set; }
    }
}
