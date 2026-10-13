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

        /// <summary>
        /// When <c>true</c>, card terminals that are only reachable locally are routed through the checkout agent
        /// (<c>AddAgentTerminals</c>, axis C). Needs an <c>IAgentConnector</c>, which the AgentServices web part
        /// brings along. Despite the class name this switch has nothing to do with the config export; it lives here
        /// because this is the web part of the provider-neutral billing library.
        /// </summary>
        public bool ActivateAgentTerminals { get; set; }

        /// <summary>
        /// Type expression of the context hosting the payments tables (a <c>DbContext</c> implementing
        /// <c>IPaymentsContext</c>) — the same notation as <c>ContextType</c> in the other parts. Required when
        /// <see cref="ActivateAgentTerminals"/> is set.
        /// </summary>
        public string? TerminalsContextType { get; set; }
    }
}
