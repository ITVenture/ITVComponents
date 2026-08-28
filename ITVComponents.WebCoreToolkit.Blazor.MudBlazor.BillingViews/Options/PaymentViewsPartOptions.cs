namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Options
{
    /// <summary>
    /// WebPart switch for the payment views (axis B). Separate from the billing-views switch because the two
    /// axes ship independently: a host may sell subscriptions without shops, run shops without subscriptions, or
    /// use the payments services with a front end of its own and none of these pages.
    /// </summary>
    public class PaymentViewsPartOptions
    {
        /// <summary>When <c>true</c>, the payout-account, sales and platform-overview pages are registered.</summary>
        public bool ActivatePaymentViews { get; set; }
    }
}
