namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments
{
    /// <summary>
    /// Lifecycle of a single end-customer sale on a tenant's connected account. Status transitions only ever
    /// move forward (the provider delivers events at-least-once and unordered), see the Connect webhook handler.
    /// </summary>
    public enum TenantSaleStatus
    {
        /// <summary>Recorded locally, hosted payment page created, not paid yet.</summary>
        Pending = 0,

        /// <summary>The end customer paid; the money is on the tenant's connected account.</summary>
        Paid = 1,

        /// <summary>The payment attempt failed (declined card, failed authentication).</summary>
        Failed = 2,

        /// <summary>The tenant or the host cancelled the sale before payment.</summary>
        Canceled = 3,

        /// <summary>The hosted payment page expired without a payment.</summary>
        Expired = 4,

        /// <summary>Fully refunded (the sum of all refunds equals the sale amount).</summary>
        Refunded = 5,

        /// <summary>Partly refunded (refunds exist, but their sum is below the sale amount).</summary>
        PartiallyRefunded = 6
    }
}
