namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration
{
    /// <summary>
    /// WebPart activation options for the billing system-config export section. When
    /// <see cref="ActivateBillingConfigExport"/> is set, the WebPart contributes the billing catalog as a
    /// <c>ConfigExtensionMarkup</c> subtype (via <c>AddBillingConfigExtension</c>), so the downloadable system
    /// configuration includes the plans/add-ons section. Requires a config-handler host (TenantSecurity) whose
    /// context also implements <c>IBillingContext</c>.
    /// </summary>
    public class BillingConfigExportPartOptions
    {
        /// <summary>
        /// When <c>true</c>, the billing catalog is registered as a system-config export section and round-trips
        /// in the downloadable system configuration. Off by default so a host that does not want billing in its
        /// system config simply omits it (the base section stays non-polymorphic and serializes without billing).
        /// </summary>
        public bool ActivateBillingConfigExport { get; set; }
    }
}
