using System;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Impl
{
    internal static class StripeMapping
    {
        /// <summary>Converts a decimal amount to Stripe minor units (cents).</summary>
        public static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

        /// <summary>Maps our interval to a Stripe recurring interval, or null for one-time prices.</summary>
        public static string? ToStripeInterval(BillingInterval interval) => interval switch
        {
            BillingInterval.Monthly => "month",
            BillingInterval.Yearly => "year",
            _ => null
        };

        /// <summary>Maps a Stripe subscription status string to <see cref="SubscriptionStatus"/>.</summary>
        public static SubscriptionStatus ToSubscriptionStatus(string? stripeStatus) => stripeStatus switch
        {
            "trialing" => SubscriptionStatus.Trialing,
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "incomplete" => SubscriptionStatus.Incomplete,
            "incomplete_expired" => SubscriptionStatus.Canceled,
            "unpaid" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.None
        };
    }
}
