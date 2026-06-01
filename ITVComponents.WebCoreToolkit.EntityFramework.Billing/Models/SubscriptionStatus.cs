namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// Lifecycle state of a <see cref="TenantSubscription"/>, mirrored from the payment provider via webhook.
    /// </summary>
    public enum SubscriptionStatus
    {
        None,
        Trialing,
        Active,
        PastDue,
        Canceled,
        Incomplete
    }
}
