namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// Domain predicates over <see cref="TenantSubscription"/>. These live here — next to the entity, not next to
    /// a consumer — because the UI and the checkout guard MUST agree on what counts as a subscription: if the page
    /// hides the plan list while the guard still lets a checkout through (or the other way round), the tenant either
    /// gets a second parallel provider subscription or is locked out of subscribing at all.
    /// </summary>
    public static class TenantSubscriptionExtensions
    {
        /// <summary>
        /// True when the row mirrors a subscription that currently binds the tenant at the provider.
        /// </summary>
        /// <remarks>
        /// A row exists as soon as the first checkout is *started* — it pins the provider customer to the tenant and
        /// carries <see cref="TenantSubscription.ProviderCustomerId"/> but no subscription id yet (status
        /// <see cref="SubscriptionStatus.None"/>). That row is a customer note, not a subscription: an aborted
        /// checkout leaves it behind for good, and treating it as a subscription strands the tenant on a
        /// "current subscription: None" screen with no way back to the plan list.
        /// <para><see cref="SubscriptionStatus.Canceled"/> is deliberately NOT live either: the row keeps its
        /// subscription id after cancellation, so counting it would strand the tenant the same way, one step later.
        /// A canceled row still holds the customer reference and the history — hence kept, just not binding.</para>
        /// <para><see cref="SubscriptionStatus.Incomplete"/> IS live: the provider subscription exists and awaits
        /// payment confirmation, so starting a second checkout would create a parallel one.</para>
        /// </remarks>
        public static bool IsLive(this TenantSubscription? subscription)
            => subscription is
               {
                   Status: SubscriptionStatus.Active
                        or SubscriptionStatus.Trialing
                        or SubscriptionStatus.PastDue
                        or SubscriptionStatus.Incomplete
               }
               && !string.IsNullOrEmpty(subscription.ProviderSubscriptionId);
    }
}
